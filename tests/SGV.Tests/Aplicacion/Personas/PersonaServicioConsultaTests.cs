using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class PersonaServicioConsultaTests
{
    private static readonly Persona PersonaActiva = new("Juan", "Pérez", "LEG-001", "juan@test.com")
    {
        Id = Guid.Parse("60000000-0000-0000-0000-000000000001")
    };

    private static Persona CrearPersonaInactiva()
    {
        var p = new Persona("Ana", "García", "LEG-002", "ana@test.com")
        {
            Id = Guid.Parse("60000000-0000-0000-0000-000000000002")
        };
        p.Desactivar();
        return p;
    }

    [Fact]
    public async Task ListAsync_CuandoExistenPersonasActivas_RetornaListaDeDto()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(PersonaActiva.Id, dto.Id);
        Assert.Equal(PersonaActiva.Legajo, dto.Legajo);
        Assert.Equal(PersonaActiva.Nombres, dto.Nombres);
        Assert.Equal(PersonaActiva.Apellidos, dto.Apellidos);
        Assert.Equal(PersonaActiva.Email, dto.Email);
        Assert.Equal(PersonaActiva.TipoDocumento, dto.TipoDocumento);
        Assert.Equal(PersonaActiva.NumeroDocumento, dto.NumeroDocumento);
        Assert.Equal(PersonaActiva.Telefono, dto.Telefono);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenPersonas_RetornaListaVacia()
    {
        var repo = new FakePersonaRepository { Datos = [] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListAsync_ExcluyePersonasInactivas()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, CrearPersonaInactiva()] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        Assert.Equal(PersonaActiva.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaActivaExiste_RetornaDto()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(PersonaActiva.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(PersonaActiva.Id, resultado!.Id);
        Assert.Equal(PersonaActiva.Nombres, resultado.Nombres);
        Assert.Equal(PersonaActiva.Apellidos, resultado.Apellidos);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaNoExiste_RetornaNull()
    {
        var repo = new FakePersonaRepository { Datos = [] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaEstaInactiva_RetornaNull()
    {
        var personaInactiva = CrearPersonaInactiva();
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, personaInactiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(personaInactiva.Id, default);

        Assert.Null(resultado);
    }

    // ===================== ListarAsync tests =====================

    [Fact]
    public async Task ListarAsync_ConSegmentoActivas_RetornaSoloActivos()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListarAsync(
            new PersonaListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        Assert.Single(resultado.Items);
        Assert.Equal(PersonaActiva.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task ListarAsync_ConSegmentoEliminadas_RetornaSoloEliminadas()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, CrearPersonaInactiva()] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListarAsync(
            new PersonaListQuery(1, 10, null, null, PersonaSegmentoListado.Eliminadas),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Single(resultado.Items);
        Assert.NotEqual(PersonaActiva.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task ListarAsync_SegmentosNoSeMezclan()
    {
        var otra = new Persona("Otro", "Apellido", "LEG-OTRA", "otra@test.com")
        {
            Id = Guid.Parse("60000000-0000-0000-0000-000000000099")
        };
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, otra] };
        otra.Desactivar();
        var servicio = new PersonaServicioConsulta(repo);

        var resultadoActivas = await servicio.ListarAsync(
            new PersonaListQuery(1, 10, null, null, PersonaSegmentoListado.Activas), default);
        var resultadoEliminadas = await servicio.ListarAsync(
            new PersonaListQuery(1, 10, null, null, PersonaSegmentoListado.Eliminadas), default);

        Assert.Equal(1, resultadoActivas.TotalCount);
        Assert.Equal(1, resultadoEliminadas.TotalCount);
        Assert.Equal(PersonaActiva.Id, Assert.Single(resultadoActivas.Items).Id);
        Assert.Equal(otra.Id, Assert.Single(resultadoEliminadas.Items).Id);
        Assert.DoesNotContain(resultadoActivas.Items, p => p.Id == otra.Id);
        Assert.DoesNotContain(resultadoEliminadas.Items, p => p.Id == PersonaActiva.Id);
    }

    [Fact]
    public async Task ListarAsync_TotalCountProvieneDelRepositorio()
    {
        var personas = Enumerable.Range(0, 25)
            .Select(i => new Persona($"N{i}", $"A{i}", $"LEG-{i:000}", $"p{i}@test.com")
            {
                Id = Guid.Parse($"60000000-0000-0000-0000-{i:D12}")
            })
            .ToArray();
        var repo = new FakePersonaRepository { Datos = personas.ToList() };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListarAsync(
            new PersonaListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(25, resultado.TotalCount);
        Assert.Equal(10, resultado.Items.Count);
    }

    [Fact]
    public async Task ListarAsync_ConSortApellidosDesc_OrdenaServidorAntesDePaginar()
    {
        // Personas con nombres invertidos vs apellidos para probar que el sort
        // server-side precede al Skip/Take (REQ-CM-01). Si el sort se aplicara
        // solo en la página recibida, los apellidos no respetarían el orden.
        var repo = new FakePersonaRepository();
        var p1 = new Persona("Ana",   "Zulu",   "LEG-1", "a@x.com") { Id = Guid.NewGuid() };
        var p2 = new Persona("Beto",  "Yankee", "LEG-2", "b@x.com") { Id = Guid.NewGuid() };
        var p3 = new Persona("Carla", "Xray",   "LEG-3", "c@x.com") { Id = Guid.NewGuid() };
        repo.Datos.AddRange([p1, p2, p3]);
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListarAsync(
            new PersonaListQuery(1, 10, null, "apellidos_desc"),
            default);

        Assert.Equal(new[] { "Zulu", "Yankee", "Xray" },
            resultado.Items.Select(i => i.Apellidos).ToArray());
    }

    [Fact]
    public async Task ListarAsync_ConSortInvalido_CaeAApellidosAsc()
    {
        // Cualquier sort desconocido cae al default (apellidos_asc) por
        // consistencia con Cargos y para preservar el contrato de paginación.
        var repo = new FakePersonaRepository();
        var p1 = new Persona("Zoe",  "Zulu",   "LEG-1", "a@x.com") { Id = Guid.NewGuid() };
        var p2 = new Persona("Yago", "Alpha",  "LEG-2", "b@x.com") { Id = Guid.NewGuid() };
        repo.Datos.AddRange([p1, p2]);
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListarAsync(
            new PersonaListQuery(1, 10, null, "foo_bar"),
            default);

        Assert.Equal(new[] { "Alpha", "Zulu" },
            resultado.Items.Select(i => i.Apellidos).ToArray());
    }
}

internal sealed class FakePersonaRepository : IPersonaRepository
{
    public List<Persona> Datos { get; set; } = [];

    public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id && e.IsActive));
    }

    public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Persona>>(Datos.Where(e => e.IsActive).ToList());
    }

    public Task AddAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
        bool? soloSinUsuario = null,
        CancellationToken cancellationToken = default)
    {
        // Mirror production predicate so the service unit tests can assert
        // segmented pagination/sort behavior without a real DB.
        var filtered = Datos.Where(p =>
            segmento == PersonaSegmentoListado.Activas
                ? p.IsActive
                : (!p.IsActive));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            filtered = filtered.Where(p =>
                (p.Legajo?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                || p.Nombres.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || p.Apellidos.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || (p.Email?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.NumeroDocumento?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Mirror production sort: apply server-side BEFORE Skip/Take so
        // pagination respects the visible ordering.
        var ordered = ApplySort(filtered, sort).ToList();
        var totalCount = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Persona>, int)>((items, totalCount));
    }

    private static IOrderedEnumerable<Persona> ApplySort(IEnumerable<Persona> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "legajo_desc" => source.OrderByDescending(static p => p.Legajo, StringComparer.OrdinalIgnoreCase),
            "legajo_asc" => source.OrderBy(static p => p.Legajo, StringComparer.OrdinalIgnoreCase),
            "apellidos_desc" => source.OrderByDescending(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase)
                                     .ThenByDescending(static p => p.Nombres, StringComparer.OrdinalIgnoreCase),
            "apellidos_asc" => source.OrderBy(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(static p => p.Nombres, StringComparer.OrdinalIgnoreCase),
            "nombres_desc" => source.OrderByDescending(static p => p.Nombres, StringComparer.OrdinalIgnoreCase),
            "nombres_asc" => source.OrderBy(static p => p.Nombres, StringComparer.OrdinalIgnoreCase),
            "email_desc" => source.OrderByDescending(static p => p.Email, StringComparer.OrdinalIgnoreCase),
            "email_asc" => source.OrderBy(static p => p.Email, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(static p => p.Nombres, StringComparer.OrdinalIgnoreCase)
        };
}
