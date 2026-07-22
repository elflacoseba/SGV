using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Personas;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests de seam HTTP del subrecurso <c>persona-skill</c> sobre
/// <see cref="PersonaApiClient"/>. Slice 2 del change
/// <c>implementa-persona-habilidades</c>.
///
/// Cubren el comportamiento observable del cliente HTTP contra un
/// <see cref="HttpMessageHandler"/> mockeado: rutas, métodos, body
/// JSON deserializado, manejo de 404 (estado vacío recuperable),
/// manejo de 4xx/5xx via el mapper común (<see cref="ErrorCategoria"/>),
/// propagación de excepciones de transporte y cancelación cooperativa.
/// Espejo de <c>CargoSkillApiClientTests</c>.
/// </summary>
public class PersonaSkillApiClientTests
{
    [Fact]
    public async Task GetSkillsAsync_Http200WithPayload_ReturnsParsedDtosAndHitsSubresourceRoute()
    {
        // AC persona-skill-query-contract Req 1: cada item del subrecurso
        // expone Skill (HabilidadDto anidado) y Nivel (NivelHabilidadDto
        // anidado). El cliente conserva la estructura sin filtrar campos
        // y respeta la ruta /api/v1/personas/{personaId}/skills.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "C-001", "Habilidad", null, "Cat");
        var nivel = new NivelHabilidadDto(nivelId, "JR", "Junior", 1, 1);
        var payload = new[]
        {
            new PersonaSkillDetailDto(habilidad, nivel)
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(personaId);

        Assert.Single(result);
        Assert.Equal(habilidad, result[0].Skill);
        Assert.Equal(nivel, result[0].Nivel);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{personaId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetSkillsAsync_Http404_ReturnsEmptyListWithoutThrowing()
    {
        // AC persona-skill-ui-tabla-editable Req 2 escenario "Persona sin
        // habilidades": si la persona ya no existe, el endpoint responde
        // 404; el cliente debe tratarlo como estado vacío, NO como un
        // error fatal. La grilla parte del estado vacío legible.
        var personaId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(personaId);

        Assert.Empty(result);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{personaId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute()
    {
        // AC persona-skill-asignar-editar Req 2: el PUT devuelve el DTO
        // del vínculo persistido (SkillId, NivelId). El cliente debe
        // serializar la request SIN agregar personaId/skillId al body
        // (esos viven en la ruta).
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new PersonaSkillDto(skillId, nivelId);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(nivelId);

        var result = await client.UpsertSkillAsync(personaId, skillId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(skillId, result.Value!.SkillId);
        Assert.Equal(nivelId, result.Value.NivelId);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{personaId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        // AC persona-skill-validation-feedback Req 1: cuando el backend
        // emite un ValidationProblemDetails con 'errors', el cliente debe
        // traducirlo a PersonaSkillCommandResult.Failure con FieldErrors
        // poblado bajo la misma clave. Esto alimenta la Razor Page para
        // que el error aparezca junto al input correcto.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["nivelId"] = new[] { "El nivel de habilidad es obligatorio." }
        })
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Uno o más campos del vínculo contienen errores de validación."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(Guid.Empty);

        var result = await client.UpsertSkillAsync(personaId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal(PersonaSkillErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("nivelId", result.FieldErrors!.Keys);
        Assert.Equal(
            "El nivel de habilidad es obligatorio.",
            result.FieldErrors!["nivelId"][0]);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithoutErrors_ReturnsFailureWithValidationCategoria()
    {
        // AC persona-skill-validation-feedback Req 1 escenario degradado:
        // un 400 con ProblemDetails plano (sin 'errors') debe seguir
        // devolviendo un Failure tipado Validation, sin FieldErrors
        // poblado (la Razor Page lo renderiza como mensaje global).
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 400,
            Title = "NivelHabilidadNoExiste",
            Detail = "El nivel de habilidad referenciado no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(personaId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal(PersonaSkillErrorType.Validation, result.Error.Type);
        Assert.Equal("NivelHabilidadNoExiste", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http404_ReturnsFailureWithNotFoundCategoria()
    {
        // AC persona-skill-asignar-editar Req 3 escenario "Persona
        // inexistente": si la persona o la habilidad referenciada no
        // existen, el backend responde 404 y el cliente lo traduce a
        // NotFound (no a Validation), para que la UI lo distinga del
        // error de validación.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "PersonaNoEncontrada",
            Detail = "La persona no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(personaId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.NotFound, result.Error!.Categoria);
        Assert.Equal(PersonaSkillErrorType.NotFound, result.Error.Type);
        Assert.Equal("PersonaNoEncontrada", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http200WithEmptyBody_ReturnsFailureWithEmptyBodyCode()
    {
        // PR3a review follow-up (R1): si el backend responde 200 con
        // body vacío, ReadFromJsonAsync devuelve null y el código
        // caería con NRE. El cliente captura el caso y devuelve un
        // Failure tipado Validation/EmptyBody para que la Razor Page
        // pueda mostrar "El servidor respondió 200 sin payload." en
        // vez de propagar una NullReferenceException.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var emptyResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json")
        };
        var handler = new RecordingHandler(_ => emptyResponse);
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(personaId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal("EmptyBody", result.Error.Code);
        Assert.Equal("El servidor respondió 200 sin payload.", result.Error.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http204_ReturnsDeleteSuccessAndHitsDeleteSubresourceRoute()
    {
        // AC persona-skill-ui-tabla-editable Req 4 escenario "Quitar una
        // habilidad": el DELETE debe responder 204 y el cliente lo
        // traduce a un DeleteResult con Succeeded=true.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(personaId, skillId);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{personaId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http404WithProblemDetails_ReturnsFailureWithNotFoundCategoria()
    {
        // AC persona-skill-ui-tabla-editable Req 4 escenario degradado:
        // si el DELETE responde 404 (asociación inexistente), el cliente
        // lo traduce a Succeeded=false con Categoria=NotFound y
        // Code/Message del ProblemDetails.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "AsociacionNoEncontrada",
            Detail = "La asociación entre la persona y la habilidad no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(personaId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
        Assert.Equal("AsociacionNoEncontrada", result.Code);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http401_ReturnsFailureWithUnauthorizedCategoria()
    {
        // PR3a review follow-up (R3): 401 → Categoria=Unauthorized
        // para que la Razor Page pueda redirigir a login ante una sesión
        // expirada. Slice 2 (#125): el cliente ya delega en
        // CommandResultMapper.Map (defaults "Unauthorized" / "Su sesión
        // expiró. Vuelva a iniciar sesión.").
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(personaId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Categoria);
        Assert.Equal("Unauthorized", result.Code);
        Assert.Equal("Su sesión expiró. Vuelva a iniciar sesión.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http403_ReturnsFailureWithForbiddenCategoria()
    {
        // PR3a review follow-up (R3): 403 → Categoria=Forbidden para
        // distinguir "usuario autenticado sin rol" de un error genérico
        // de servidor. La página muestra el mensaje "Acceso denegado."
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(personaId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal(ErrorCategoria.Forbidden, result.Categoria);
        Assert.Equal("Forbidden", result.Code);
        Assert.Equal("Acceso denegado.", result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task DeleteSkillAsync_Http5xx_ReturnsFailureWithTransportCategoria(HttpStatusCode status)
    {
        // Slice 2 (#125): 5xx → Categoria=Transport con defaults del
        // mapper ("TransportError" / "El servicio no respondió
        // correctamente. Intentá nuevamente."). El cliente delega en
        // DeleteResultMapper.BuildDeleteResultAsync; ya no bifurca en
        // un switch privado.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new PersonaApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(personaId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(ErrorCategoria.Transport, result.Categoria);
        Assert.Equal("TransportError", result.Code);
        Assert.Equal("El servicio no respondió correctamente. Intentá nuevamente.", result.Message);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task GetSkillsAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // web-apiclient-transport-contract: la falla de transporte del
        // subrecurso (TaskCanceled o HttpRequest) debe propagarse sin
        // que el cliente la silencie. La Razor Page atrapa esto en
        // OnGetAsync y muestra un mensaje recuperable.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.GetSkillsAsync(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task UpsertSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PersonaApiClient(NewHttpClient(handler));
        var request = new AsignarPersonaSkillRequest(Guid.NewGuid());

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.UpsertSkillAsync(Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task DeleteSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteSkillAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        // Contrato de cancelación cooperativa: un CancellationToken
        // pre-cancelado NO debe disparar el envío HTTP; el cliente debe
        // arrojar OperationCanceledException antes de tocar la red.
        var handler = new RecordingHandler();
        var client = new PersonaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid(), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
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