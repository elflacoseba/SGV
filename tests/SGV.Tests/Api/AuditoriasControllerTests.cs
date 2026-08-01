using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Auditoria;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Tests S2 del módulo de auditoría (controller API admin-only).
///
/// Tareas cubiertas:
///   2.1 — Auth (401 anónimo, 403 sin rol, 200 admin), paginación +
///         filtros, detalle 200/404, JSON sin old/new, [Authorize]
///         por reflexión.
///   2.2 — <c>DateFrom &gt; DateTo</c> → 400 Validation con ProblemDetails.
///
/// En STRICT TDD, este archivo es la fase RED: el tipo
/// <c>SGV.Api.Controllers.AuditoriasController</c> aún NO existe; el archivo
/// NO compila hasta que la fase GREEN lo introduzca.
///
/// Para evitar acoplar estos tests a MySQL, cada test que toca el servicio
/// instala <see cref="FakeAuditoriaServicioConsulta"/> vía
/// <see cref="ApiWebApplicationFactory.WithOverrides"/>. El fake simula el
/// contrato del servicio real: filtra, pagina, valida el rango de fechas
/// y lanza la misma <see cref="ArgumentException"/> que la impl EF.
/// Los tests sin acceso a datos (auth, reflexión) usan el factory raíz.
/// </summary>
[Collection("ApiIntegration")]
public sealed class AuditoriasControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public AuditoriasControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string BasePath = "/api/v1/auditorias";

    // ====================================================================
    // 2.1.a/b — Auth: 401 sin credenciales, 403 sin rol Administrador
    // ====================================================================

    /// <summary>
    /// 2.1.a — Un cliente sin credenciales recibe <c>401 Unauthorized</c>
    /// al pedir el listado. No requiere fake (el filtro de auth corre
    /// antes de invocar al servicio).
    /// </summary>
    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var client = _fixture.RootFactory.CreateClient();

        var response = await client.GetAsync(BasePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 2.1.b — Un cliente autenticado sin rol <c>Administrador</c> recibe
    /// <c>403 Forbidden</c>. El handler fake distingue admin de user
    /// por token (<see cref="FakeAuthenticationDefaults"/>).
    /// </summary>
    [Fact]
    public async Task Get_NonAdmin_Returns403()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync(BasePath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ====================================================================
    // 2.1.c — Admin: 200 con PagedResult<AuditoriaDto> shape correcto
    // ====================================================================

    /// <summary>
    /// 2.1.c — Un administrador recibe <c>200 OK</c> con un body que
    /// contiene las 4 propiedades canónicas de <see cref="PagedResult{T}"/>:
    /// <c>Items</c>, <c>TotalCount</c>, <c>Page</c>, <c>PageSize</c>.
    /// </summary>
    [Fact]
    public async Task Get_Admin_Returns200WithPagedResult()
    {
        var id = Guid.NewGuid();
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta(MakeSeedAuditoriaDto(id)));
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var response = await http.GetAsync(BasePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var resultado = JsonSerializer.Deserialize<PagedResult<AuditoriaDto>>(json, JsonOptions);
        Assert.NotNull(resultado);
        Assert.NotNull(resultado!.Items);
        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(20, resultado.PageSize);
        Assert.Single(resultado.Items);
        Assert.Equal(id, resultado.Items[0].Id);
    }

    // ====================================================================
    // 2.1.d — Paginación + filtros: ?entityName=...&page=1&pageSize=10
    // ====================================================================

    /// <summary>
    /// 2.1.d — La query <c>?entityName=Cargo&amp;page=1&amp;pageSize=10</c>
    /// filtra el conjunto de filas sembradas y pagina correctamente.
    /// Verifica que el controller reenvía los filtros al servicio y
    /// devuelve la página solicitada. <c>FakeAuditoriaServicioConsulta</c>
    /// aplica el mismo contrato de filtrado+paginación que la impl EF.
    /// </summary>
    [Fact]
    public async Task Get_Admin_PaginacionYFiltrosAplican()
    {
        var seed = new List<AuditoriaDto>
        {
            // 3 filas Cargo, 1 fila Persona, 1 fila Habilidad
            MakeAuditoriaDto("Cargo",     "Alta",         "u1", new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            MakeAuditoriaDto("Cargo",     "Modificacion", "u1", new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc)),
            MakeAuditoriaDto("Cargo",     "BajaLogica",   "u2", new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)),
            MakeAuditoriaDto("Persona",   "Alta",         "u2", new DateTime(2026, 1, 13, 0, 0, 0, DateTimeKind.Utc)),
            MakeAuditoriaDto("Habilidad", "Alta",         "u3", new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc)),
        };
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta(seed));
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var response = await http.GetAsync(BasePath + "?entityName=Cargo&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var resultado = JsonSerializer.Deserialize<PagedResult<AuditoriaDto>>(json, JsonOptions);
        Assert.NotNull(resultado);
        Assert.Equal(3, resultado!.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        Assert.Equal(3, resultado.Items.Count);
        Assert.All(resultado.Items, dto => Assert.Equal("Cargo", dto.EntityName));

        // PageSize chico: pide 2 → la página trae 2 de las 3.
        var responsePag2 = await http.GetAsync(BasePath + "?entityName=Cargo&page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, responsePag2.StatusCode);
        var resultado2 = JsonSerializer.Deserialize<PagedResult<AuditoriaDto>>(
            await responsePag2.Content.ReadAsStringAsync(), JsonOptions);
        Assert.Equal(3, resultado2!.TotalCount);
        Assert.Equal(2, resultado2.Items.Count);
    }

    // ====================================================================
    // 2.1.e — Detalle: 200 con DTO existente, 404 con id inexistente
    // ====================================================================

    /// <summary>
    /// 2.1.e.1 — Un id presente en el fake devuelve <c>200 OK</c> con
    /// el DTO correspondiente.
    /// </summary>
    [Fact]
    public async Task GetById_Admin_Existe_200()
    {
        var id = Guid.NewGuid();
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta(MakeSeedAuditoriaDto(id)));
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var response = await http.GetAsync($"{BasePath}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = JsonSerializer.Deserialize<AuditoriaDto>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(id, dto!.Id);
        Assert.Equal("Persona", dto.EntityName);
    }

    /// <summary>
    /// 2.1.e.2 — Un id sin fila correspondiente devuelve <c>404 Not Found</c>.
    /// </summary>
    [Fact]
    public async Task GetById_Admin_NoExiste_404()
    {
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta());
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var response = await http.GetAsync($"{BasePath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ====================================================================
    // 2.1.f — JSON wire NO contiene OldValuesJson ni NewValuesJson
    // ====================================================================

    /// <summary>
    /// 2.1.f — El body JSON del listado expone las claves del wire
    /// contract (<c>ChangedPropertiesJson</c> + el resto) pero NO contiene
    /// <c>OldValuesJson</c> ni <c>NewValuesJson</c> en ningún nivel. Es
    /// el guardrail D-2 (no exponer PII) observado a través del controller.
    /// </summary>
    [Fact]
    public async Task Get_Json_NoContieneOldNiNewValues()
    {
        var id = Guid.NewGuid();
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta(MakeSeedAuditoriaDto(id)));
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var response = await http.GetAsync(BasePath);
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("OldValuesJson", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NewValuesJson", json, StringComparison.Ordinal);
        Assert.DoesNotContain("oldValuesJson", json, StringComparison.Ordinal);
        Assert.DoesNotContain("newValuesJson", json, StringComparison.Ordinal);
        // System.Text.Json wire default = camelCase; el nombre de la
        // propiedad en el DTO es ChangedPropertiesJson.
        Assert.Contains("changedPropertiesJson", json, StringComparison.Ordinal);
    }

    // ====================================================================
    // 2.1.g — [Authorize(Roles = Administrador)] por reflexión
    // ====================================================================

    /// <summary>
    /// 2.1.g — <see cref="AuditoriasController"/> lleva
    /// <see cref="AuthorizeAttribute"/> a nivel de clase con
    /// <c>Roles = Administrador</c>. Es el guardrail D-1 (admin-only)
    /// observado vía reflexión, sin tocar la pipeline.
    /// </summary>
    [Fact]
    public void AuditoriasController_TieneAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.AuditoriasController);

        var authorizeAttribute = controllerType
            .GetCustomAttribute<AuthorizeAttribute>(inherit: true);

        Assert.NotNull(authorizeAttribute);
        Assert.NotNull(authorizeAttribute!.Roles);
        // Roles viaja como CSV ("Administrador" o "Administrador,GestorVacantes");
        // assert.Contains(string, string) hace substring, suficiente para
        // verificar que "Administrador" está presente.
        Assert.Contains(RolesSgv.Administrador, authorizeAttribute.Roles!);
    }

    // ====================================================================
    // 2.2 — DateFrom > DateTo → 400 Validation con ProblemDetails
    // ====================================================================

    /// <summary>
    /// 2.2.a — <c>?dateFrom=2026-02-01&amp;dateTo=2026-01-01</c>
    /// (rango invertido) hace que el fake lance <see cref="ArgumentException"/>
    /// con el mensaje del servicio; el controller la mapea a <c>400</c> con
    /// un <see cref="ProblemDetails"/> cuyo <c>detail</c> contiene
    /// <c>"rango"</c> o <c>"DateFrom"</c>.
    /// </summary>
    [Fact]
    public async Task Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails()
    {
        var cliente = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuditoriaServicioConsulta>();
            services.AddSingleton<IAuditoriaServicioConsulta>(
                new FakeAuditoriaServicioConsulta());
        });
        await using var clienteDisposable = cliente;
        var http = clienteDisposable.CreateAdminClient();

        var url = BasePath + "?dateFrom=2026-02-01T00:00:00Z&dateTo=2026-01-01T00:00:00Z";
        var response = await http.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        // Validamos que es un ProblemDetails (status=400) sin atarnos al
        // nombre concreto del tipo (ProblemDetails vs ValidationProblemDetails):
        // ambos descienden de ProblemDetails y comparten Status/Detail/Title.
        var problem = JsonSerializer.Deserialize<ProblemDetails>(json, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.NotNull(problem.Detail);
        Assert.NotEmpty(problem.Detail!);
        Assert.True(
            problem.Detail!.Contains("rango", StringComparison.OrdinalIgnoreCase)
            || problem.Detail.Contains("DateFrom", StringComparison.OrdinalIgnoreCase),
            $"El detail debe contener 'rango' o 'DateFrom'. Actual: '{problem.Detail}'.");
    }

    // ====================================================================
    // Helpers — datos sembrados y fake del servicio de consulta
    // ====================================================================

    private static AuditoriaDto MakeAuditoriaDto(
        string entityName,
        string operation,
        string userId,
        DateTime occurredAt,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            entityName,
            operation,
            occurredAt,
            userId,
            $"{userId}-name",
            "[\"Nombre\"]",
            Guid.NewGuid());

    private static IEnumerable<AuditoriaDto> MakeSeedAuditoriaDto(Guid id)
    {
        yield return new AuditoriaDto(
            id,
            "Persona",
            "Modificacion",
            new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            "u1",
            "u1-name",
            "[\"Nombre\"]",
            Guid.NewGuid());
    }

    /// <summary>
    /// Fake del puerto de lectura que simula el contrato del servicio
    /// real (filtrado, orden, paginación, validación de rango) sin
    /// tocar EF/MySQL. Mantiene la misma excepción
    /// <see cref="ArgumentException"/> que la impl cuando
    /// <c>DateFrom &gt; DateTo</c> para que el controller la mapee
    /// idénticamente al wire real.
    /// </summary>
    private sealed class FakeAuditoriaServicioConsulta : IAuditoriaServicioConsulta
    {
        private const int MaxPageSize = 100;
        private const int MinPageSize = 1;

        private readonly IReadOnlyList<AuditoriaDto> _data;

        public FakeAuditoriaServicioConsulta(IEnumerable<AuditoriaDto>? seed = null)
        {
            _data = seed?.ToList() ?? [];
        }

        public Task<PagedResult<AuditoriaDto>> QueryAsync(
            AuditoriaListQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (query.DateFrom.HasValue && query.DateTo.HasValue
                && query.DateFrom.Value > query.DateTo.Value)
            {
                throw new ArgumentException(
                    $"El rango de fechas es inválido: DateFrom ({query.DateFrom:o}) es posterior a DateTo ({query.DateTo:o}). "
                    + "DateFrom debe ser menor o igual a DateTo.",
                    nameof(query));
            }

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < MinPageSize
                ? MinPageSize
                : (query.PageSize > MaxPageSize ? MaxPageSize : query.PageSize);

            IEnumerable<AuditoriaDto> filtered = _data;
            if (!string.IsNullOrWhiteSpace(query.EntityName))
                filtered = filtered.Where(a => a.EntityName == query.EntityName);
            if (!string.IsNullOrWhiteSpace(query.Operation))
                filtered = filtered.Where(a => a.Operation == query.Operation);
            if (query.DateFrom.HasValue)
                filtered = filtered.Where(a => a.OccurredAt >= query.DateFrom.Value);
            if (query.DateTo.HasValue)
                filtered = filtered.Where(a => a.OccurredAt <= query.DateTo.Value);
            if (!string.IsNullOrWhiteSpace(query.UserId))
                filtered = filtered.Where(a => a.UserId == query.UserId);
            if (query.CorrelationId.HasValue)
                filtered = filtered.Where(a => a.CorrelationId == query.CorrelationId.Value);

            // Sort dinámico server-side (espejo del switch del servicio
            // real). Default fecha_desc; valor no reconocido cae al
            // default sin error.
            IOrderedEnumerable<AuditoriaDto> ordered = query.Sort switch
            {
                "fecha_asc" => filtered.OrderBy(a => a.OccurredAt),
                "fecha_desc" => filtered.OrderByDescending(a => a.OccurredAt),
                "entidad_asc" => filtered.OrderBy(a => a.EntityName),
                "entidad_desc" => filtered.OrderByDescending(a => a.EntityName),
                "operacion_asc" => filtered.OrderBy(a => a.Operation),
                "operacion_desc" => filtered.OrderByDescending(a => a.Operation),
                "usuario_asc" => filtered.OrderBy(a => a.UserId),
                "usuario_desc" => filtered.OrderByDescending(a => a.UserId),
                "correlacion_asc" => filtered.OrderBy(a => a.CorrelationId),
                "correlacion_desc" => filtered.OrderByDescending(a => a.CorrelationId),
                _ => filtered.OrderByDescending(a => a.OccurredAt)
            };
            ordered = ordered.ThenByDescending(a => a.Id);

            var materialized = ordered.ToList();
            var totalCount = materialized.Count;
            var items = materialized
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<AuditoriaDto>(items, totalCount, page, pageSize));
        }

        public Task<AuditoriaDetalleDto?> GetDetalleDtoAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var dto = _data.FirstOrDefault(a => a.Id == id);
            if (dto is null)
            {
                return Task.FromResult<AuditoriaDetalleDto?>(null);
            }

            // Proyecta al DTO enriquecido preservando los metadatos del
            // wire contract de listado y rellenando EntityId +
            // OldValuesJson + NewValuesJson con valores plausibles (los
            // tests existentes no inspeccionan estos campos, sólo la
            // presencia del id).
            var detalle = new AuditoriaDetalleDto(
                dto.Id,
                dto.EntityName,
                Guid.NewGuid().ToString(),
                dto.Operation,
                dto.OccurredAt,
                dto.UserId,
                dto.UserName,
                dto.CorrelationId,
                dto.ChangedPropertiesJson,
                OldValuesJson: "{\"old\":1}",
                NewValuesJson: "{\"new\":2}");
            return Task.FromResult<AuditoriaDetalleDto?>(detalle);
        }
    }
}