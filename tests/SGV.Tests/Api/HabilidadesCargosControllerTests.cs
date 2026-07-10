using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// PR-WU-A: tests RED→GREEN del nuevo subrecurso
/// <c>GET /api/v1/skills/{skillId}/cargos</c>. Cubre los 8 escenarios del
/// design §5.1 sin tocar la base de datos real: el
/// <see cref="ISkillCargoServicioConsulta"/> se sustituye por un fake en
/// memoria vía <see cref="ApiWebApplicationFactory"/>, y el
/// <see cref="IHabilidadServicioConsulta"/> por defecto de la factory ya
/// devuelve la habilidad semilla o null según el id solicitado.
/// </summary>
public sealed class HabilidadesCargosControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    // ---- Fake service ----

    /// <summary>
    /// Fake en memoria del servicio de consulta. Replica segmentación,
    /// búsqueda, orden y paginación server-side para que el controller
    /// pueda ejercitarse sin tocar EF Core ni MySQL.
    /// </summary>
    internal sealed class FakeSkillCargoServicioConsulta : ISkillCargoServicioConsulta
    {
        public static readonly Guid DefaultHabilidadId = FakeHabilidadServicio.HabilidadId1;

        // Semillas con códigos ordenables para verificar ASC/DESC y bandera
        // de soft-delete por item (skill-cargo-query-contract Req 1).
        public static readonly SkillCargoDetailDto Active1 = Make(
            Guid.Parse("ba000000-0000-0000-0000-000000000001"), "CAR-001", "Director");
        public static readonly SkillCargoDetailDto Active2 = Make(
            Guid.Parse("ba000000-0000-0000-0000-000000000002"), "CAR-002", "Gerente");
        public static readonly SkillCargoDetailDto Active3 = Make(
            Guid.Parse("ba000000-0000-0000-0000-000000000003"), "CAR-003", "Analista");
        public static readonly SkillCargoDetailDto Deleted1 = Make(
            Guid.Parse("bb000000-0000-0000-0000-000000000001"), "ELIM-001", "Eliminado A",
            eliminado: true);
        public static readonly SkillCargoDetailDto Deleted2 = Make(
            Guid.Parse("bb000000-0000-0000-0000-000000000002"), "ELIM-002", "Eliminado B",
            eliminado: true);

        public List<SkillCargoDetailDto> Activas { get; set; } = [Active1, Active2, Active3];
        public List<SkillCargoDetailDto> Eliminadas { get; set; } = [Deleted1, Deleted2];

        public Func<Guid, HabilidadCargosListQuery, CancellationToken,
            Task<PagedResult<SkillCargoDetailDto>>>? ListarHandler { get; set; }

        public HabilidadCargosListQuery? LastQuery { get; private set; }

        public Task<PagedResult<SkillCargoDetailDto>> ListarCargosAsync(
            Guid habilidadId,
            HabilidadCargosListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;

            if (ListarHandler is not null)
            {
                return ListarHandler(habilidadId, query, cancellationToken);
            }

            var source = query.Segmento == HabilidadSegmentoListado.Eliminadas
                ? Eliminadas
                : Activas;
            var filtered = source.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var lowered = query.Search.ToLowerInvariant();
                filtered = filtered.Where(d =>
                    d.Cargo.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                    || d.Cargo.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase));
            }

            // Espejo del repo.ApplySort — el controller propaga sort tal cual
            // al servicio y el servicio lo delega al repo (ver T3).
            var sorted = (query.Sort?.ToLowerInvariant()) switch
            {
                "codigo_desc" => filtered.OrderByDescending(d => d.Cargo.Codigo).ToList(),
                "codigo_asc" => filtered.OrderBy(d => d.Cargo.Codigo).ToList(),
                "nombre_desc" => filtered.OrderByDescending(d => d.Cargo.Nombre).ToList(),
                "nombre_asc" => filtered.OrderBy(d => d.Cargo.Nombre).ToList(),
                _ => filtered.OrderBy(d => d.Cargo.Codigo).ToList()
            };

            var list = sorted.ToList();
            var total = list.Count;
            var items = list
                .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
                .Take(query.PageSize)
                .ToList();

            return Task.FromResult(new PagedResult<SkillCargoDetailDto>(items, total, query.Page, query.PageSize));
        }

        private static SkillCargoDetailDto Make(Guid cargoId, string codigo, string nombre, bool eliminado = false)
        {
            var nivelCargoId = Guid.Parse("70000000-0000-0000-0000-000000000001");
            var nivelReqId = Guid.Parse("91000000-0000-0000-0000-000000000001");
            return new SkillCargoDetailDto(
                new CargoDto(cargoId, codigo, nombre, null, nivelCargoId, "Directivo"),
                new NivelHabilidadDto(nivelReqId, "BASICO", "Básico", 1, 1))
            {
                CargoId = cargoId,
                NivelRequeridoId = nivelReqId,
                Ponderacion = 1.00m,
                EsObligatoria = false,
                CargoEliminado = eliminado
            };
        }
    }

    // ---- GET /api/v1/skills/{skillId}/cargos ----

    [Fact]
    public async Task Get_SkillExists_WithActiveCargos_Returns200WithPagedResultAndDtoItems()
    {
        // Skill-cargo-query-contract Req 1 escenario 1: habilidad existente
        // con cargos asociados → 200 OK con Items, TotalCount, Page, PageSize
        // y cada item con el DTO dedicado.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.NotNull(paged);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(3, paged.Items.Count);
        Assert.Equal(1, paged.Page);
        Assert.Equal(20, paged.PageSize);

        var first = paged.Items[0];
        Assert.NotNull(first.Cargo);
        Assert.NotNull(first.Nivel);
        Assert.Equal(FakeSkillCargoServicioConsulta.Active1.CargoId, first.CargoId);
        Assert.Equal(FakeSkillCargoServicioConsulta.Active1.NivelRequeridoId, first.NivelRequeridoId);
        Assert.Equal(1.00m, first.Ponderacion);
        Assert.False(first.EsObligatoria);
        Assert.False(first.CargoEliminado);
        Assert.Equal("CAR-001", first.Cargo.Codigo);
        Assert.Equal("Director", first.Cargo.Nombre);
        Assert.All(paged.Items, item => Assert.False(item.CargoEliminado));
    }

    [Fact]
    public async Task Get_SkillExists_WithoutCargos_Returns200WithEmptyCollection()
    {
        // Skill-cargo-query-contract Req 1 escenario 2 / habilidad-management
        // escenario "Habilidad existente sin cargos": 200 OK con Items vacío,
        // NO 404. Una colección vacía es válida cuando la habilidad existe.
        var fake = new FakeSkillCargoServicioConsulta { Activas = [], Eliminadas = [] };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.NotNull(paged);
        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.TotalCount);
    }

    [Fact]
    public async Task Get_SkillNotFound_Returns404()
    {
        // Skill-cargo-query-contract Req 3 / habilidad-management escenario
        // "Habilidad inexistente": el controller chequea la habilidad padre
        // con _servicio.GetByIdAsync antes de delegar al servicio. Si el
        // skill no existe, devuelve 404 (distingue del 200 con lista vacía).
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/skills/{Guid.NewGuid()}/cargos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        // Skill-cargo-query-contract Req 3 escenario "Acceso sin token es
        // rechazado": el [Authorize] a nivel de controller exige bearer
        // token; sin Authorization header el endpoint responde 401.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidStatus_FallsBackToActivas()
    {
        // Skill-cargo-query-contract Req 2 escenario "Status inválido cae a
        // activas": status=archivo NO devuelve 400, sino que resuelve a
        // activas y devuelve 200 con los cargos activos.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos?status=archivo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HabilidadSegmentoListado.Activas, fake.LastQuery!.Segmento);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.Equal(3, paged.Items.Count);
    }

    [Fact]
    public async Task Get_PageSizeAndPaging_ReturnsCorrectSlice()
    {
        // Skill-cargo-query-contract Req 1 + habilidad-management escenario
        // paginación: con 3 cargos y pageSize=2 page=2 se devuelven los
        // últimos items del segmento, TotalCount=3, Page=2, PageSize=2.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.Page);
        Assert.Equal(2, paged.PageSize);
        Assert.Single(paged.Items);
        Assert.Equal("CAR-003", paged.Items[0].Cargo.Codigo);
    }

    [Fact]
    public async Task Get_SortCodigoDesc_ReturnsOrderedCollection()
    {
        // Skill-cargo-query-contract Req 1 + design §5.1 escenario 7: el
        // sort=codigo_desc se propaga al servicio y el primer item tiene
        // un código mayor que el segundo.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos?sort=codigo_desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("codigo_desc", fake.LastQuery!.Sort);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.Equal("CAR-003", paged.Items[0].Cargo.Codigo);
        Assert.Equal("CAR-002", paged.Items[1].Cargo.Codigo);
        Assert.Equal("CAR-001", paged.Items[2].Cargo.Codigo);
    }

    [Fact]
    public async Task Get_StatusEliminadas_ReturnsOnlyDeletedCargos()
    {
        // Skill-cargo-query-contract Req 2 + design §5.1 escenario 8: con
        // status=eliminadas se devuelven SOLO los cargos soft-deleted
        // asociados a la habilidad, no se mezclan con los activos.
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos?status=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HabilidadSegmentoListado.Eliminadas, fake.LastQuery!.Segmento);
        var paged = await ReadAsAsync<PagedResult<SkillCargoDetailDto>>(response);
        Assert.Equal(2, paged.TotalCount);
        Assert.Equal(2, paged.Items.Count);
        Assert.All(paged.Items, item =>
        {
            Assert.NotNull(item.Cargo);
            Assert.StartsWith("ELIM-", item.Cargo.Codigo);
            Assert.True(item.CargoEliminado);
        });
    }

    // PR #88 (review 🟡2): boundary cases de normalización. SkillsController
    // normaliza page<1→1, pageSize<1→20, pageSize>100→100, status≠eliminadas→Activas
    // (case-insensitive). sort desconocido cae a codigo_asc en el repo
    // (SkillCargoRepository.ApplySort). Cubrir estos casos evita drift si
    // alguien refactoriza la normalización.
    [Theory]
    [InlineData("page=0", 1, 20)]                   // page<1 → 1
    [InlineData("page=-5", 1, 20)]                  // page negativo → 1
    [InlineData("pageSize=0", 1, 20)]               // pageSize<1 → default 20
    [InlineData("pageSize=-1", 1, 20)]              // pageSize negativo → default
    [InlineData("pageSize=999999", 1, 100)]         // pageSize fuera de rango → cap 100
    [InlineData("pageSize=101&page=1", 1, 100)]     // pageSize=101 → cap 100
    [InlineData("status=ARCHIVO", 1, 20)]           // status inválido mayúsculas → Activas
    [InlineData("status=ElImInAdAs", 1, 20)]        // status mixto → Eliminadas (case-insensitive)
    [InlineData("page=3&pageSize=10&sort=injection", 3, 10)] // sort inválido no rompe (cae a codigo_asc)
    public async Task Get_NormalizationBoundaries_Returns200WithNormalizedQuery(
        string queryString, int expectedPage, int expectedPageSize)
    {
        var fake = new FakeSkillCargoServicioConsulta();
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ISkillCargoServicioConsulta>();
            services.AddSingleton<ISkillCargoServicioConsulta>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/cargos?{queryString}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(fake.LastQuery);
        Assert.Equal(expectedPage, fake.LastQuery!.Page);
        Assert.Equal(expectedPageSize, fake.LastQuery.PageSize);
    }
}