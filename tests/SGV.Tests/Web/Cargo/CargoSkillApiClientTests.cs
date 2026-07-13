using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Cargo;

public partial class CargoApiClientTests
{
    [Fact]
    public async Task GetSkillsAsync_Http200WithPayload_ReturnsParsedDtosAndHitsSubresourceRoute()
    {
        // AC de cargo-skill-query-contract Req 1: cada item del subrecurso
        // expone Skill, Nivel, SkillId, NivelRequeridoId, Ponderacion y
        // EsObligatoria. El cliente debe conservar esos 6 campos sin filtrar.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "C-001", "Habilidad", null, "Cat");
        var nivel = new NivelHabilidadDto(nivelId, "JR", "Junior", 1, 1);
        var payload = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelId,
                Ponderacion = 2.50m,
                EsObligatoria = true
            }
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(cargoId);

        Assert.Single(result);
        Assert.Equal(skillId, result[0].SkillId);
        Assert.Equal(nivelId, result[0].NivelRequeridoId);
        Assert.Equal(2.50m, result[0].Ponderacion);
        Assert.True(result[0].EsObligatoria);
        Assert.Equal(habilidad, result[0].Skill);
        Assert.Equal(nivel, result[0].Nivel);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetSkillsAsync_Http404_ReturnsEmptyListWithoutThrowing()
    {
        // AC de cargo-skill-ui-tabla-editable Req 2 escenario "Cargo sin
        // habilidades": la grilla parte de un estado vacío legible. Si el
        // cargo ya no existe, el endpoint responde 404; el cliente debe
        // tratarlo como estado vacío, NO como un error fatal.
        var cargoId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(cargoId);

        Assert.Empty(result);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute()
    {
        // AC de cargo-skill-asignar-editar Req 2: el PUT devuelve el DTO
        // completo del vínculo persistido (SkillId, NivelRequeridoId,
        // Ponderacion, EsObligatoria). El cliente debe serializar la request
        // SIN agregar cargoId/skillId al body (esos viven en la ruta).
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new CargoSkillDto(skillId, nivelId)
        {
            Ponderacion = 1.50m,
            EsObligatoria = true
        };
        CapturedJsonBody? captured = null;
        var handler = new RecordingHandler(req =>
        {
            captured = new CapturedJsonBody(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
            return Json(HttpStatusCode.OK, dto);
        });
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId, 1.50m, true);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(skillId, result.Value!.SkillId);
        Assert.Equal(nivelId, result.Value.NivelRequeridoId);
        Assert.Equal(1.50m, result.Value.Ponderacion);
        Assert.True(result.Value.EsObligatoria);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Null(captured!.FindProperty("cargoId"));
        Assert.Null(captured.FindProperty("skillId"));
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithPonderacionFieldError_ReturnsFailureWithFieldErrors()
    {
        // AC de cargo-skill-ponderacion-obligatoria Req 4: cuando el backend
        // emite un ValidationProblemDetails con el campo 'ponderacion', el
        // cliente debe traducirlo a CargoSkillCommandResult.Failure con
        // FieldErrors poblado bajo la misma clave. Esto alimenta la Razor
        // Page para que el error aparezca junto al input correcto.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["ponderacion"] = new[] { "La ponderación no puede superar 100.00." }
        })
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Uno o más campos del vínculo contienen errores de validación."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId, 150m);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("ponderacion", result.FieldErrors!.Keys);
        Assert.Equal("La ponderación no puede superar 100.00.", result.FieldErrors!["ponderacion"][0]);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithoutErrors_ReturnsFailureWithValidationType()
    {
        // AC de cargo-skill-ponderacion-obligatoria Req 4 escenario degradado:
        // un 400 con ProblemDetails plano (sin 'errors') debe seguir
        // devolviendo un Failure tipado Validation, sin FieldErrors poblado
        // (la Razor Page lo renderiza como mensaje global, no por campo).
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 400,
            Title = "NivelHabilidadNoExiste",
            Detail = "El nivel de habilidad referenciado no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, problem));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.Validation, result.Error!.Type);
        Assert.Equal("NivelHabilidadNoExiste", result.Error.Code);
        Assert.Equal("El nivel de habilidad referenciado no existe.", result.Error.Message);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http200WithEmptyBody_ReturnsFailureWithEmptyBodyCode()
    {
        // PR3a review follow-up (R1): si el backend responde 200 con body
        // vacío, ReadFromJsonAsync devuelve null y el código actual cae con
        // NRE al aplicar `dto!`. El helper debe capturar el caso y devolver
        // un Failure tipado Validation/EmptyBody para que la Razor Page de
        // PR3b pueda mostrar "El servidor respondió 200 sin payload." en
        // vez de propagar una NullReferenceException.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var emptyResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json")
        };
        var handler = new RecordingHandler(_ => emptyResponse);
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.Validation, result.Error!.Type);
        Assert.Equal("EmptyBody", result.Error.Code);
        Assert.Equal("El servidor respondió 200 sin payload.", result.Error.Message);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http404_ReturnsFailureWithNotFound()
    {
        // AC de cargo-skill-asignar-editar Req 3 escenario "Nivel requerido
        // inexistente": si el cargo o la habilidad referenciada no existen,
        // el backend responde 404 y el cliente lo traduce a NotFound (no a
        // Validation), para que la UI lo distinga del error de validación.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "CargoNoEncontrado",
            Detail = "El cargo no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.NotFound, result.Error!.Type);
        Assert.Equal("CargoNoEncontrado", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    // ─────────────────────────────────────────────────────────────
    // PR3a follow-up — W2 helper bifurcation.
    //
    // El helper ToSkillCommandResultAsync actual colapsa 401/403/409/5xx en
    // un Validation con code "Unexpected". PR3b no podrá diferenciar acceso
    // denegado de error de servidor; recibirá siempre un mensaje genérico.
    // Esta Theory cierra el W2 del verify-report: cada código tiene que
    // traducirse a un CargoSkillErrorType distinto para que la Razor Page
    // pueda decidir entre "redirigir a login", "mostrar 403", "mostrar
    // conflicto" o "mostrar error recuperable".
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CargoSkillErrorType.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CargoSkillErrorType.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, CargoSkillErrorType.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError, CargoSkillErrorType.Transport)]
    [InlineData(HttpStatusCode.BadGateway, CargoSkillErrorType.Transport)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CargoSkillErrorType.Transport)]
    public async Task UpsertSkillAsync_NonSuccessStatus_ReturnsCorrectCargoSkillErrorType(
        HttpStatusCode status, CargoSkillErrorType expectedType)
    {
        // W2 RED: hasta que el helper bifurque los códigos, los cuatro casos
        // caen en el fallback "Unexpected" con Validation. La Theory fallará
        // hasta que el feat(web) correspondiente extienda el helper.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        // Usar un body ProblemDetails neutral para que cualquier código
        // 4xx/5xx reciba un cuerpo JSON válido y no dispare la rama de
        // parse fallido (que también cae en Unexpected). El helper debe
        // bifurcar por StatusCode independientemente del cuerpo.
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = $"Err{status}",
            Detail = "Detalle de la prueba."
        };
        var handler = new RecordingHandler(_ => Json(status, problem));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedType, result.Error!.Type);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http204_ReturnsDeleteSuccessAndHitsDeleteSubresourceRoute()
    {
        // AC de cargo-skill-ui-tabla-editable Req 4 escenario "Quitar una
        // habilidad": el DELETE debe responder 204 y el cliente lo traduce
        // a un DeleteResult con Succeeded=true.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http404WithProblemDetails_ReturnsFailureWithNotFound()
    {
        // AC de cargo-skill-ui-tabla-editable Req 4 escenario degradado: si
        // el DELETE responde 404 (asociación inexistente), el cliente lo
        // traduce a Succeeded=false con Code/Message del ProblemDetails para
        // que la grilla muestre un mensaje legible sin stack trace.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "AsociacionNoEncontrada",
            Detail = "La asociación entre el cargo y la habilidad no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("AsociacionNoEncontrada", result.Code);
        Assert.Equal("La asociación entre el cargo y la habilidad no existe.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http401_ReturnsFailureWithUnauthorized()
    {
        // PR3a review follow-up (R3): DeleteSkillAsync asimétrico con el PUT.
        // Para que la Razor Page de PR3b pueda redirigir a login ante una
        // sesión expirada, la rama 401 debe quedar bifurcada con un Code
        // por defecto ("Unauthorized") cuando el backend no entrega un
        // ProblemDetails parseable.
        //
        // Slice 2 (#125): el cliente ya no mantiene un MapSkillError
        // privado; delega en CommandResultMapper.Map. El default message
        // 401 del mapper es "Su sesión expiró. Vuelva a iniciar sesión."
        // (design §5.4 / copy canónica). Tests pre-existentes adaptados a
        // la nueva copy unificada.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Categoria);
        Assert.Equal("Unauthorized", result.Code);
        Assert.Equal("Su sesión expiró. Vuelva a iniciar sesión.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http403_ReturnsFailureWithForbidden()
    {
        // PR3a review follow-up (R3): 403 Forbidden distingue "usuario
        // autenticado sin rol" de un error genérico de servidor. La rama
        // del helper debe poblar Code="Forbidden" cuando el body no es
        // ProblemDetails, en vez de devolver Code=null.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("Forbidden", result.Code);
        Assert.Equal("Acceso denegado.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http409_ReturnsFailureWithConflict()
    {
        // PR3a review follow-up (R3): aunque el controller actual no emita
        // 409 desde este subrecurso, mantener la rama deja al cliente
        // simétrico con el PUT y preparado para una futura evolución del
        // backend (e.g. "asociación duplicada"). El helper debe poblar
        // Code="Conflict" cuando el body no trae un ProblemDetails.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("Conflict", result.Code);
        Assert.Equal("Conflicto.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http500WithJsonProblem_ReturnsFailureWithTransport()
    {
        // PR3a review follow-up (R3) + Slice 2 (#125): 5xx → StatusCode
        // preservado + Code/Message del CommandResultMapper.Map (default
        // "TransportError" / "El servicio no respondió correctamente.
        // Intentá nuevamente." cuando el body no es ProblemDetails
        // parseable). El cliente delega; ya no bifurca en `MapSkillError`
        // privado.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(ErrorCategoria.Transport, result.Categoria);
        Assert.Equal("TransportError", result.Code);
        Assert.Equal("El servicio no respondió correctamente. Intentá nuevamente.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http400WithNonJsonBody_ReturnsFailureWithoutCrashing()
    {
        // PR3a review follow-up (R3): un 4xx con body no-JSON NO debe
        // tirar JsonException sin capturar. Además, el Code/Message por
        // defecto ("BadRequest" / "Solicitud inválida.") debe poblar el
        // resultado para que la Razor Page tenga un fallback legible
        // aunque el backend entregue HTML de error en vez de un
        // ProblemDetails.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("BadRequest", result.Code);
        Assert.Equal("Solicitud inválida.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http500WithNonJsonBody_ReturnsFailureWithoutCrashing()
    {
        // AC de cargo-skill-ui-tabla-editable Req 5: errores 5xx deben
        // traducirse en un Failure con StatusCode preservado, sin filtrar
        // stack traces al usuario. Slice 2 (#125): el mapper común
        // provee defaults "TransportError" / "El servicio no respondió
        // correctamente. Intentá nuevamente." — copy canónica del helper.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(ErrorCategoria.Transport, result.Categoria);
        Assert.Equal("TransportError", result.Code);
        Assert.Equal("El servicio no respondió correctamente. Intentá nuevamente.", result.Message);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task DeleteSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // AC de web-apiclient-transport-contract: la falla de transporte del
        // subrecurso (TaskCanceled o HttpRequest) debe propagarse sin que el
        // cliente la silencie. La Razor Page atrapa esto en handlers y
        // muestra un mensaje recuperable.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task UpsertSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // AC de web-apiclient-transport-contract aplicado al subrecurso
        // PUT: la falla de transporte (TaskCanceled o HttpRequest) debe
        // propagarse como excepción nativa, no enmascararse en un Failure.
        // Cubrir ambas ramas asegura que la Razor Page distingue
        // cancelación cooperativa de un corte real del backend.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.UpsertSkillAsync(Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task DeleteSkillAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        // Contrato de cancelación cooperativa: un CancellationToken
        // pre-cancelado NO debe disparar el envío HTTP; el cliente debe
        // arrojar OperationCanceledException antes de tocar la red.
        var handler = new RecordingHandler();
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid(), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    // ──────────────────────────────────────────────
    // Slice 2 (#125) — migración CargoSkill al mapper común.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task UpsertSkillAsync_Http403WithNonJsonBody_FallsBackToForbiddenDefaults()
    {
        // Sin ProblemDetails parseable, el mapper usa defaults Forbidden / Acceso denegado.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error!.Categoria);
        Assert.Equal(CargoSkillErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Forbidden", result.Error.Code);
        Assert.Equal("Acceso denegado.", result.Error.Message);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task UpsertSkillAsync_TransportFails_PropagatesNativeException_NotCategoriaTransport(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.UpsertSkillAsync(Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task DeleteSkillAsync_TransportFails_PropagatesNativeException_NotCategoriaTransport(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteSkillAsync_PreCanceledToken_PropagatesOperationCanceledException()
    {
        var handler = new RecordingHandler();
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.DeleteSkillAsync(
                Guid.NewGuid(), Guid.NewGuid(),
                new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }
}
