using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Personas;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests de seam HTTP del <see cref="PersonaApiClient"/> contra un
/// <see cref="HttpMessageHandler"/> mockeado. Espejo de
/// <c>CargoApiClientBasicTests</c> para el módulo de Personas: cubren
/// las rutas (GET <c>/api/v1/personas</c>, GET <c>/{id}</c>, DELETE
/// <c>/{id}</c>, POST, PUT, PATCH <c>/reactivar</c>), el contrato de
/// paginación (<c>/consulta?page=...&pageSize=...&status=...&sort=...</c>)
/// y la matriz de errores del issue #125.
/// </summary>
public class PersonaApiClientBasicTests
{
    [Fact]
    public async Task GetAllAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { new PersonaDto(id, "L-001", "Ana", "García", null, null, null, null, null, null, true) };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Ana", result[0].Nombres);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/personas", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var id = Guid.NewGuid();
        var payload = new PersonaDto(id, "L-002", "Juan", "Pérez", "juan@example.com", null, null, "DNI", "28999888", null, true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Juan", result!.Nombres);
        Assert.Equal("Pérez", result.Apellidos);
        Assert.Equal($"/api/v1/personas/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        // AC: el shell trata 404 como "no disponible" recuperable
        // (DetailsPage / EditPage), no como excepción. El cliente debe
        // traducirlo a null.
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DesactivarAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DesactivarAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DesactivarAsync_Http409WithProblemDetails_ReturnsFailedWithConflictCategoria()
    {
        // AC: 409 (legajo duplicado / email duplicado / documento
        // duplicado) debe traducirse a PersonaDeleteResult con
        // Categoria=Conflict para que el PageModel pueda ramificar
        // correctamente.
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "LegajoDuplicado",
            Detail = "Ya existe una persona activa con el legajo L-DUP."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DesactivarAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("LegajoDuplicado", result.Code);
        Assert.Equal("Ya existe una persona activa con el legajo L-DUP.", result.Message);
        Assert.Equal(ErrorCategoria.Conflict, result.Categoria);
    }

    [Fact]
    public async Task CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute()
    {
        var newId = Guid.NewGuid();
        var dto = new PersonaDto(newId, "L-NEW", "Nueva", "Persona", null, null, null, null, null, null, true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Created, dto));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var request = new CrearPersonaRequest("L-NEW", "Nueva", "Persona");
        var result = await client.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(newId, result.Value!.Id);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/personas", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        // AC: el backend serializa camelCase; el cliente debe preservar
        // los FieldErrors en el CommandResult para que el PageModel los
        // mapee a ModelState con el prefix "Input." (PersonaFormHelpers).
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["legajo"] = new[] { "El legajo es obligatorio." },
            ["email"] = new[] { "Email inválido." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var request = new CrearPersonaRequest(string.Empty, "Ana", "García");
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PersonaErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("legajo", result.FieldErrors!.Keys);
        Assert.Contains("email", result.FieldErrors!.Keys);
    }

    [Fact]
    public async Task UpdateAsync_Http200_ReturnsDtoAndHitsPutRoute()
    {
        var id = Guid.NewGuid();
        var dto = new PersonaDto(id, "L-001", "Ana Editada", "García", "ana@example.com", null, null, "DNI", "30123456", null, true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var request = new ActualizarPersonaRequest("L-001", "Ana Editada", "García", "ana@example.com");
        var result = await client.UpdateAsync(id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("Ana Editada", result.Value.Nombres);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task QueryAsync_WithStatusEliminadas_SerializesStatusInUri()
    {
        // AC: el segmento Eliminadas se serializa como `status=eliminadas`
        // en el query string; cualquier otro valor (incluido Activas y
        // default) omite el parámetro para que la API caiga a activas.
        var payload = new PersonaListadoDto(
            [new PersonaDto(Guid.NewGuid(), "DEL-001", "Eliminada", "Persona", null, null, null, null, null, null, false)],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.QueryAsync(new PersonaListQuery(1, 20, null, null, PersonaSegmentoListado.Eliminadas));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/personas/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("status=eliminadas", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutStatusOrSearchOrSort_DoesNotIncludeThemInUri()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new PersonaListadoDto([], 0, 1, 20)));
        var client = new PersonaApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new PersonaListQuery(1, 20, null, null, PersonaSegmentoListado.Activas));

        Assert.Equal("/api/v1/personas/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("page=1", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=20", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("search=", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sort=", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithSearchAndSort_SerializesBothInUri()
    {
        // AC: search y sort deben viajar en query string para que el
        // backend aplique filtros ANTES del Skip/Take. Si el cliente no
        // los serializa, la paginación con búsqueda se rompe entre
        // páginas.
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new PersonaListadoDto([], 0, 1, 20)));
        var client = new PersonaApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new PersonaListQuery(1, 10, "garcia", "apellidos_asc", PersonaSegmentoListado.Activas));

        Assert.Equal("/api/v1/personas/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("search=garcia", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_asc", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivarAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = new PersonaDto(id, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.ReactivarAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{id}/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivarAsync_OnConflict_ReturnsConflictResult()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "LegajoDuplicado",
            Detail = "Ya existe una persona activa con el legajo L-001."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.ReactivarAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PersonaErrorType.Conflict, result.Error!.Type);
        Assert.Equal("LegajoDuplicado", result.Error.Code);
    }

    [Fact]
    public async Task GetTiposDocumentoAsync_Http200WithPayload_ReturnsParsedCatalogAndHitsRoute()
    {
        // AC: issue #147 PR3 — el cliente consume GET /api/v1/tipos-documento.
        // Espejo de CargoApiClient.GetNivelesAsync (BasicTests): happy path,
        // ruta absoluta, body deserializado a TipoDocumentoDto.
        var dniId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var payload = new[]
        {
            new TipoDocumentoDto(dniId, "DNI", "Documento Nacional de Identidad", "^\\d{7,8}$", 7, 8),
            new TipoDocumentoDto(Guid.NewGuid(), "LE", "Libreta de Enrolamiento", "^\\d{6,8}$", 6, 8)
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetTiposDocumentoAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("DNI", result[0].Codigo);
        Assert.Equal(dniId, result[0].Id);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/tipos-documento", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetTiposDocumentoAsync_Http200EmptyBody_ReturnsEmptyList()
    {
        // AC: si el backend responde 200 con body vacío (null deserializado),
        // el cliente devuelve una lista vacía en vez de propagar
        // JsonException — analog al patrón de CargoApiClient.QueryAsync
        // cuando la paginación viene vacía.
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.OK, null));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetTiposDocumentoAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ErrorCategoria.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCategoria.Forbidden)]
    [InlineData(HttpStatusCode.RequestTimeout, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.InternalServerError, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.BadGateway, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ErrorCategoria.Transport)]
    public async Task CreateAsync_NonSuccessStatus_ReturnsFailureWithCorrectCategoria(
        HttpStatusCode status, ErrorCategoria expectedCategoria)
    {
        // Matriz REQ-2 (issue #125): cada status debe mapear a la
        // categoria correspondiente de ErrorCategoria para que los
        // PageModels puedan ramificar correctamente.
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = $"Err{status}",
            Detail = $"Detalle del status {status}."
        };
        var handler = new RecordingHandler(_ => Json(status, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.CreateAsync(new CrearPersonaRequest("L-001", "Ana", "García"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedCategoria, result.Error!.Categoria);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task QueryAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // web-apiclient-transport-contract: el cliente NO convierte
        // excepciones nativas del pipeline HTTP a CommandResult.Transport;
        // las propaga para que el PageModel las capture vía
        // TransportFailureClassifier y muestre un error recuperable.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new PersonaListQuery(1, 20, null, null, PersonaSegmentoListado.Activas)));
    }

    [Fact]
    public async Task QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        var handler = new RecordingHandler();
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryAsync(
                new PersonaListQuery(1, 20, null, null, PersonaSegmentoListado.Activas),
                new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task QueryAsync_WithSoloSinUsuarioTrue_SerializesSoloSinUsuarioInUri()
    {
        // AC WU-4 (D-02/D-04): cuando `SoloSinUsuario == true`, el cliente
        // serializa `&soloSinUsuario=true` en el query string para que la
        // API aplique el anti-join contra `AspNetUsers.PersonaId`. El
        // valor debe viajar sin doble-encoding (Uri.EscapeDataString no
        // aplica a `true` literal, sólo al valor).
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new PersonaListadoDto([], 0, 1, 25)));
        var client = new PersonaApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new PersonaListQuery(
            Page: 1,
            PageSize: 25,
            Search: null,
            Sort: null,
            Segmento: PersonaSegmentoListado.Activas,
            SoloSinUsuario: true));

        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("soloSinUsuario=true", query, StringComparison.OrdinalIgnoreCase);
        // No debe haber doble-encoding: `%5C` es un backslash escapado y
        // no debe aparecer cuando se serializa `true` literal.
        Assert.DoesNotContain("%5C", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithSoloSinUsuarioNullOrFalse_OmitsParameter()
    {
        // AC WU-4 (back-compat REQ-PM-01): `SoloSinUsuario` ausente,
        // `null` o `false` MUST omitir el parámetro del query string para
        // preservar el contrato de los consumidores vigentes (Index
        // Personas, typeahead) que no envían el flag.
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new PersonaListadoDto([], 0, 1, 20)));
        var client = new PersonaApiClient(NewHttpClient(handler));

        // Caso 1: null (default).
        _ = await client.QueryAsync(new PersonaListQuery(
            Page: 1, PageSize: 20, Search: null, Sort: null, Segmento: PersonaSegmentoListado.Activas));
        Assert.DoesNotContain(
            "soloSinUsuario",
            handler.LastRequest?.RequestUri?.Query ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Caso 2: false explícito.
        _ = await client.QueryAsync(new PersonaListQuery(
            Page: 1, PageSize: 20, Search: null, Sort: null,
            Segmento: PersonaSegmentoListado.Activas, SoloSinUsuario: false));
        Assert.DoesNotContain(
            "soloSinUsuario",
            handler.LastRequest?.RequestUri?.Query ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task QueryAsync_WithSoloSinUsuarioTrue_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // AC WU-4 (web-apiclient-transport-contract): la introducción de
        // `soloSinUsuario` en BuildQueryUri no debe agregar try-catch
        // espurios. La excepción nativa del pipeline HTTP debe burbujear
        // tal cual para que el PageModel la clasifique vía
        // TransportFailureClassifier y muestre un error recuperable.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new PersonaListQuery(
                Page: 1, PageSize: 25, Search: null, Sort: null,
                Segmento: PersonaSegmentoListado.Activas, SoloSinUsuario: true)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }
}