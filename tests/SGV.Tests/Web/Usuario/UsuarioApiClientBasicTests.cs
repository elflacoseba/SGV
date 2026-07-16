using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Usuarios;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Tests de seam HTTP del <see cref="UsuarioApiClient"/> contra un
/// <see cref="HttpMessageHandler"/> mockeado. Cubren las rutas (GET
/// <c>/api/v1/usuarios</c>, GET <c>/consulta</c>, GET <c>/{id}</c>,
/// POST, PUT, DELETE, PATCH <c>/reactivar</c>, GET <c>/{userId}/roles</c>),
/// el contrato de paginación y la matriz de errores del issue #125.
/// Espejo de <c>PersonaApiClientBasicTests</c> con el agregado del
/// shape Identity User (id es string, no Guid) y los códigos de
/// dominio <c>AutoBaja</c>, <c>PersonaInactiva</c>,
/// <c>UserNameDuplicado</c>, <c>EmailDuplicado</c>.
/// </summary>
public class UsuarioApiClientBasicTests
{
    [Fact]
    public async Task GetAllActivasAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute()
    {
        var personaId = Guid.NewGuid();
        var payload = new[]
        {
            new UsuarioDto("u-1", personaId, "agarcía", "agarcía@example.com",
                new[] { "Administrador" }, Nombres: "Ana", Apellidos: "García")
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.GetAllActivasAsync();

        Assert.Single(result);
        Assert.Equal("u-1", result[0].Id);
        Assert.Equal("Ana", result[0].Nombres);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var personaId = Guid.NewGuid();
        var payload = new UsuarioDto(
            "u-2", personaId, "jperez", "jperez@example.com",
            new[] { "GestorVacantes" }, Nombres: "Juan", Apellidos: "Pérez");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync("u-2");

        Assert.NotNull(result);
        Assert.Equal("Juan", result!.Nombres);
        Assert.Equal("Pérez", result.Apellidos);
        Assert.Equal($"/api/v1/usuarios/u-2", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        // AC: el shell trata 404 como "no disponible" recuperable
        // (DetailsPage / EditPage), no como excepción. El cliente debe
        // traducirlo a null.
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync("u-404");

        Assert.Null(result);
    }

    [Fact]
    public async Task DesactivarAsync_Http200_ReturnsSuccessAndHitsDeleteRoute()
    {
        // Backend PR1 expone 200 con DTO en DELETE (no 204) para
        // soportar la rama AutoBaja/Permissions en code que pueda
        // inspeccionar el body. El cliente tipado lo trata como éxito.
        var personaId = Guid.NewGuid();
        var payload = new UsuarioDto(
            "u-3", personaId, "ladmin", "ladmin@example.com",
            new[] { "Administrador" }, Nombres: "L", Apellidos: "Admin");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.DesactivarAsync("u-3");

        Assert.True(result.IsSuccess);
        Assert.Equal("u-3", result.Value!.Id);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/usuarios/u-3", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DesactivarAsync_Http403AutoBaja_ReturnsFailureWithForbiddenCategoriaAndAutoBajaCode()
    {
        // AC: AutoBaja se traduce a Forbidden en el mapper común
        // (regla D-01). Si el mapper común NO lo cubre, el backend ya
        // emite 403 con ProblemDetails.Title="AutoBaja" igual.
        var problem = new ProblemDetails
        {
            Status = 403,
            Title = "AutoBaja",
            Detail = "No podés darte de baja a vos mismo."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Forbidden, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.DesactivarAsync("u-self");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error!.Categoria);
        Assert.Equal(UsuarioErrorType.Validation, result.Error.Type);
        Assert.Equal("AutoBaja", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute()
    {
        var newUserId = "u-new";
        var personaId = Guid.NewGuid();
        var dto = new UsuarioDto(
            newUserId, personaId, "anuevo", "anuevo@example.com",
            new[] { "Consultor" }, Nombres: "Ana Nuevo", Apellidos: "User");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Created, dto));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new CrearUsuarioRequest(personaId, "anuevo", "anuevo@example.com", "Pwd!12345",
            new[] { "Consultor" });
        var result = await client.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(newUserId, result.Value!.Id);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithValidationErrorCategoria()
    {
        // PR2-HALL-1 (mini-PR correctivo): tras extender el contrato,
        // la rúbrica observable del cliente es que un 400 +
        // ValidationProblemDetails sigue mapeando a
        // `ErrorCategoria.Validation` con `Code="ValidationError"`
        // y `Message="Datos inválidos."`. La propagación de los
        // errores por-campo se cubre en
        // `CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrorsPopulated`.
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["userName"] = new[] { "El nombre de usuario ya está en uso." },
            ["personaId"] = new[] { "Debe seleccionar una persona activa." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new CrearUsuarioRequest(Guid.NewGuid(), string.Empty, "bad", "Pwd!12345",
            new[] { "Consultor" });
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(UsuarioErrorType.Validation, result.Error!.Type);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Equal("ValidationError", result.Error.Code);
        Assert.Equal("Datos inválidos.", result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrorsPopulated()
    {
        // PR2-HALL-1 (mini-PR correctivo): cuando el backend
        // responde 400 + ValidationProblemDetails con clave `errors`,
        // el `UsuarioApiClient` debe propagar el diccionario por-campo
        // en `UsuarioCommandResult.FieldErrors` para que la Razor Page
        // de Create/Edit (PR 4) pueda aplicar los mensajes junto a
        // cada control del formulario. Espejo del
        // `CargoApiClient.CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`.
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["userName"] = new[] { "El nombre de usuario ya está en uso." },
            ["personaId"] = new[] { "Debe seleccionar una persona activa." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new CrearUsuarioRequest(Guid.NewGuid(), "dup", "bad@example.com", "Pwd!12345",
            new[] { "Consultor" });
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FieldErrors);
        Assert.Equal(2, result.FieldErrors!.Count);
        Assert.Contains("userName", result.FieldErrors.Keys);
        Assert.Contains("personaId", result.FieldErrors.Keys);
        Assert.Equal("El nombre de usuario ya está en uso.", result.FieldErrors["userName"][0]);
        Assert.Equal("Debe seleccionar una persona activa.", result.FieldErrors["personaId"][0]);
        // El mapper común debe seguir produciendo Categoria
        // Validation; el PageModel discrimina primero por Categoria
        // y después inspecciona FieldErrors.
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
    }

    [Fact]
    public async Task CreateAsync_Http400ValidationProblemDetailsWithEmptyErrors_CollapsesToFailureWithoutFieldErrors()
    {
        // AC: cuando el backend responde 400 + ValidationProblemDetails
        // con clave `errors` pero sin entradas, el contrato canónico
        // del repo (`CargoApiClient`, `PuestosApiClient`,
        // `UnidadOrganizativaApiClient`, `HabilidadApiClient`,
        // `PersonaApiClient`) trata el FieldErrors vacío igual que
        // null: el cliente cae al factory simple `Failure(error)` y
        // el mapper delega al `Error.Message` (bajo la clave vacía).
        // Esto preserva la invariante "shape Validation sin per-field"
        // ≡ "shape ProblemDetails plano" para la PageModel.
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>())
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new CrearUsuarioRequest(Guid.NewGuid(), "u", "u@example.com", "Pwd!12345",
            new[] { "Consultor" });
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Null(result.FieldErrors);
        // El fallback al Error.Message sigue activo: el banner
        // estándar de feedback de error recuperable debe poder
        // mostrar el mensaje.
        Assert.Equal("Datos inválidos.", result.Error!.Message);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
    }

    [Fact]
    public async Task UpdateAsync_Http409WithProblemDetails_ReturnsFailureWithConflictAndNullFieldErrors()
    {
        // AC: cuando la respuesta es 409 + ProblemDetails plano (sin
        // clave `errors`), el cliente debe mapear a `Failure(error)`
        // con `FieldErrors == null` (no un diccionario vacío) para
        // permitir que la PageModel distinga "shape Validation sin
        // per-field" de "shape ProblemDetails plano".
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "UserNameDuplicado",
            Detail = "Ya existe un usuario activo con el mismo UserName."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new ActualizarUsuarioRequest("duplicado", "ok@example.com",
            new[] { "Administrador" });
        var result = await client.UpdateAsync("u-conflict", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("UserNameDuplicado", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpdateAsync_Http200_ReturnsDtoAndHitsPutRoute()
    {
        var personaId = Guid.NewGuid();
        var dto = new UsuarioDto(
            "u-4", personaId, "anuevo2", "anuevo2@example.com",
            new[] { "Administrador", "GestorVacantes" }, Nombres: "Ana", Apellidos: "Editada");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new ActualizarUsuarioRequest("anuevo2", "anuevo2@example.com",
            new[] { "Administrador", "GestorVacantes" });
        var result = await client.UpdateAsync("u-4", request);

        Assert.True(result.IsSuccess);
        Assert.Equal("u-4", result.Value!.Id);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/usuarios/u-4", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpdateAsync_Http409UserNameDuplicado_ReturnsFailureWithConflictCategoria()
    {
        // AC: dos PUT simultáneos que colisionan en UserName. El
        // backend responde 409 + ProblemDetails con Title="UserNameDuplicado"
        // y Detail con el mensaje accionable. El cliente tipado lo
        // traduce a Conflict categoria + código UserNameDuplicado.
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "UserNameDuplicado",
            Detail = "Ya existe un usuario activo con el mismo UserName."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var request = new ActualizarUsuarioRequest("duplicado", "ana@example.com", new[] { "Administrador" });
        var result = await client.UpdateAsync("u-conflict", request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal(UsuarioErrorType.Conflict, result.Error.Type);
        Assert.Equal("UserNameDuplicado", result.Error.Code);
    }

    [Fact]
    public async Task QueryAsync_WithStatusBloqueadas_SerializesStatusInUri()
    {
        // AC: el segmento Bloqueadas se serializa como `status=bloqueadas`
        // en el query string; cualquier otro valor (incluido Activas y
        // default) omite el parámetro para que la API caiga a activas.
        //
        // Cambio 2026-07-15-quita-soft-delete-usuario: el segmento
        // `Eliminadas` (basado en IsDeleted) se renombra a `Bloqueadas`
        // (basado en LockoutEnd). El alias `Eliminadas` se conserva
        // temporalmente en Phase 1 y se retira en Phase 3.
        var payload = new UsuarioListadoDto(
            new PagedResult<UsuarioDto>(
                Items: new[]
                {
                    new UsuarioDto("u-bloq", Guid.NewGuid(), "bloqueado", "b@example.com",
                        new[] { "Consultor" }, Nombres: "B", Apellidos: "Bloqueado")
                },
                TotalCount: 1,
                Page: 1,
                PageSize: 20));
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.QueryAsync(new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Bloqueadas));

        Assert.Single(result.Result.Items);
        Assert.Equal(1, result.Result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("status=bloqueadas", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutStatusOrSearchOrSort_DoesNotIncludeThemInUri()
    {
        var emptyPayload = new UsuarioListadoDto(
            new PagedResult<UsuarioDto>(Items: Array.Empty<UsuarioDto>(), TotalCount: 0, Page: 1, PageSize: 20));
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, emptyPayload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Activas));

        Assert.Equal("/api/v1/usuarios/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
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
        // backend aplique filtros ANTES del Skip/Take.
        var emptyPayload = new UsuarioListadoDto(
            new PagedResult<UsuarioDto>(Items: Array.Empty<UsuarioDto>(), TotalCount: 0, Page: 1, PageSize: 20));
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, emptyPayload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new UsuarioListQuery(1, 10, "garcia", "userName_asc", UsuarioSegmentoListado.Activas));

        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("search=garcia", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=userName_asc", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivarAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var personaId = Guid.NewGuid();
        var dto = new UsuarioDto(
            "u-reac", personaId, "reactivado", "r@example.com",
            new[] { "Administrador" }, Nombres: "Reac", Apellidos: "Tivado");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.ReactivarAsync("u-reac");

        Assert.True(result.IsSuccess);
        Assert.Equal("u-reac", result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/usuarios/u-reac/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivarAsync_Http409PersonaInactiva_ReturnsFailureWithConflictCategoria()
    {
        // AC: D-02 (regla de reactivación). Si la Persona vinculada está
        // IsDeleted=1, el backend responde 409 con
        // Title="PersonaInactiva". El mapper lo lleva a Conflict
        // categoria con code "PersonaInactiva" para que el banner de
        // feedback sea accionable.
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "PersonaInactiva",
            Detail = "La persona asociada está dada de baja; reactivala antes."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.ReactivarAsync("u-eli-inactive");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("PersonaInactiva", result.Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ErrorCategoria.Unauthorized)]
    [InlineData(HttpStatusCode.RequestTimeout, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.InternalServerError, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.BadGateway, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.NotFound, ErrorCategoria.NotFound)]
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
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.CreateAsync(new CrearUsuarioRequest(
            Guid.NewGuid(), "u-test", "test@example.com", "Pwd!12345", new[] { "Consultor" }));

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
        var client = new UsuarioApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Activas)));
    }

    [Fact]
    public async Task QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        var handler = new RecordingHandler();
        var client = new UsuarioApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryAsync(
                new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Activas),
                new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task DeleteAsync_WhenInvokedThroughInterface_DelegatesToDesactivarAsync()
    {
        // AC: el alias `DeleteAsync` se define como default interface
        // method sobre IUsuarioApiClient; el typed-client tipado
        // (concreto `UsuarioApiClient`) NO lo expone como método público,
        // sólo lo cumple vía la interface. Para preservar el guard de
        // contrato, ejercitamos la interface explícitamente en vez de
        // la clase concreta.
        var personaId = Guid.NewGuid();
        var dto = new UsuarioDto(
            "u-aliased", personaId, "al", "al@example.com",
            new[] { "Administrador" });
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        IUsuarioApiClient client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync("u-aliased");

        Assert.True(result.IsSuccess);
        Assert.Equal($"/api/v1/usuarios/u-aliased", handler.LastRequest?.RequestUri?.AbsolutePath);
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
