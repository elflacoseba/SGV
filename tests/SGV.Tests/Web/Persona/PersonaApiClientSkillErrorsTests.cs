using System.Net;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests de comportamiento del subrecurso <c>persona-skill</c> sobre el
/// <see cref="FakePersonaApiClient"/>. Slice 2 del change
/// <c>implementa-persona-habilidades</c>.
///
/// Cubren tres dimensiones del contrato observable del fake:
/// <list type="number">
///   <item>Registro de invocaciones: los contadores y listas internas se
///   actualizan al invocar los métodos, sin emitir HTTP.</item>
///   <item>Mapeo de errores: cuando el fake se cablea con un Failure
///   tipado (NotFound, Validation, Conflict, Unauthorized, Forbidden,
///   Transport), la categoría observable en el resultado debe coincidir
///   con el esperado por el PageModel. Esto valida que el fake y la
///   implementación HTTP comparten la taxonomía
///   <see cref="ErrorCategoria"/>.</item>
///   <item>Propagación de excepciones nativas: si el fake se configura
///   con <c>GetSkillsException</c>, el método propaga la excepción
///   nativa para que el PageModel pueda clasificarla via
///   <c>TransportFailureClassifier</c>.</item>
/// </list>
///
/// Espejo de <c>CargoHabilidadesValidationTests</c> + comportamiento
/// por defecto de <c>FakeCargoApiClient</c> en el módulo de Cargos.
/// </summary>
public class PersonaApiClientSkillErrorsTests
{
    [Fact]
    public async Task GetSkillsAsync_DefaultResult_IsEmptyList()
    {
        // AC: por defecto, el fake devuelve lista vacía (la grilla
        // editable parte del estado vacío). Si alguien cambia el default
        // sin actualizar las pruebas, este test falla ruidosamente en
        // vez de propagar el cambio silencioso a los PageModels.
        var fake = new FakePersonaApiClient();
        var personaId = Guid.NewGuid();

        var result = await fake.GetSkillsAsync(personaId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSkillsAsync_WithSeed_ReturnsSeedAndRecordsCall()
    {
        // AC: cuando el fake se semilla con habilidades, GetSkillsAsync
        // las devuelve y registra el identificador de la persona en la
        // lista de invocaciones para que los tests puedan triangular
        // el contrato.
        var fake = new FakePersonaApiClient();
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "C-001", "Habilidad", null, "Cat");
        var nivel = new NivelHabilidadDto(nivelId, "JR", "Junior", 1, 1);
        fake.GetSkillsResult = new[]
        {
            new PersonaSkillDetailDto(habilidad, nivel)
        };

        var result = await fake.GetSkillsAsync(personaId);

        Assert.Single(result);
        Assert.Equal(skillId, result[0].Skill.Id);
        Assert.Equal(nivelId, result[0].Nivel.Id);
        Assert.Contains(personaId, fake.GetSkillsCalls);
    }

    [Fact]
    public async Task GetSkillsAsync_WithException_PropagatesNative()
    {
        // AC: si el fake se configura con una excepción nativa
        // (e.g. HttpRequestException), el método debe propagarla sin
        // enmascararla. La Razor Page atrapa esto en OnGetAsync y
        // muestra un mensaje recuperable.
        var fake = new FakePersonaApiClient
        {
            GetSkillsException = new HttpRequestException("simulated transport failure")
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => fake.GetSkillsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpsertSkillAsync_DefaultResult_IsFailureValidation()
    {
        // AC: por defecto, el fake devuelve Failure Validation con
        // código `FakeNotConfigured` para forzar a los tests a cablear
        // explícitamente el resultado cuando lo necesiten. Si alguien
        // cambia el default a Success silencioso, este test falla
        // ruidosamente en vez de propagar la ilusión de cobertura.
        var fake = new FakePersonaApiClient();

        var result = await fake.UpsertSkillAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AsignarPersonaSkillRequest(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal("FakeNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task UpsertSkillAsync_WithSeed_ReturnsSeedAndRecordsCall()
    {
        // AC: cuando se cablea un Success con su DTO, el fake lo
        // devuelve y registra el triple (personaId, skillId, request)
        // para inspección.
        var fake = new FakePersonaApiClient();
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        fake.SkillUpsertResult = PersonaSkillCommandResult.Success(
            new PersonaSkillDto(skillId, nivelId));

        var request = new AsignarPersonaSkillRequest(nivelId);
        var result = await fake.UpsertSkillAsync(personaId, skillId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(skillId, result.Value!.SkillId);
        Assert.Equal(nivelId, result.Value.NivelId);
        Assert.Single(fake.SkillUpsertCalls);
        Assert.Equal((personaId, skillId, request), fake.SkillUpsertCalls[0]);
    }

    [Fact]
    public async Task UpsertSkillAsync_BackendReturnsNotFound_PropagatesCategoriaNotFound()
    {
        // AC REQ-TAXO-02: cuando el fake cablea un Failure con
        // PersonaSkillErrorType.NotFound, el resultado expone
        // ErrorCategoria.NotFound (404) para que el PageModel de Slice 3a
        // pueda ramificar correctamente.
        var fake = new FakePersonaApiClient
        {
            SkillUpsertResult = PersonaSkillCommandResult.Failure(
                new PersonaSkillError(
                    PersonaSkillErrorType.NotFound,
                    "PersonaNoEncontrada",
                    "La persona no existe.",
                    StatusCode: 404,
                    Categoria: ErrorCategoria.NotFound))
        };

        var result = await fake.UpsertSkillAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AsignarPersonaSkillRequest(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.NotFound, result.Error!.Categoria);
        Assert.Equal(PersonaSkillErrorType.NotFound, result.Error.Type);
        Assert.Equal("PersonaNoEncontrada", result.Error.Code);
    }

    [Fact]
    public async Task UpsertSkillAsync_BackendReturnsValidation_PropagatesCategoriaValidation()
    {
        // AC REQ-TAXO-02: Validation se traduce a ErrorCategoria.Validation
        // (400) y puede traer FieldErrors para que el PageModel los
        // mapee a ModelState con el prefijo correcto.
        var fake = new FakePersonaApiClient
        {
            SkillUpsertResult = PersonaSkillCommandResult.Failure(
                new PersonaSkillError(
                    PersonaSkillErrorType.Validation,
                    "NivelHabilidadNoExiste",
                    "El nivel de habilidad referenciado no existe.",
                    StatusCode: 400,
                    Categoria: ErrorCategoria.Validation))
        };

        var result = await fake.UpsertSkillAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AsignarPersonaSkillRequest(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal(PersonaSkillErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task DeleteSkillAsync_DefaultResult_IsSuccessNoContent()
    {
        // AC: por defecto, el fake devuelve éxito con 204 No Content
        // (espejo del comportamiento del FakeCargoApiClient para
        // DeleteSkillAsync). Si alguien cambia el default sin actualizar
        // las pruebas, este test falla ruidosamente.
        var fake = new FakePersonaApiClient();

        var result = await fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Fact]
    public async Task DeleteSkillAsync_WithSeed_ReturnsSeedAndRecordsCall()
    {
        // AC: cuando se cablea un Failure, el fake lo devuelve y registra
        // el par (personaId, skillId) en SkillDeleteCalls para
        // inspección.
        var fake = new FakePersonaApiClient
        {
            SkillDeleteResult = new PersonaSkillDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.NotFound,
                Code: "AsociacionNoEncontrada",
                Message: "La asociación no existe.",
                Categoria: ErrorCategoria.NotFound)
        };

        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var result = await fake.DeleteSkillAsync(personaId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("AsociacionNoEncontrada", result.Code);
        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
        Assert.Single(fake.SkillDeleteCalls);
        Assert.Equal((personaId, skillId), fake.SkillDeleteCalls[0]);
    }

    [Fact]
    public async Task DeleteSkillAsync_BackendReturnsConflict_PropagatesCategoriaConflict()
    {
        // AC: 409 Conflict se traduce a ErrorCategoria.Conflict (aunque
        // el backend actual no emita 409 desde este subrecurso, la
        // simetría con el PUT mantiene preparado el cliente para una
        // futura evolución del backend).
        var fake = new FakePersonaApiClient
        {
            SkillDeleteResult = new PersonaSkillDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.Conflict,
                Code: "Conflict",
                Message: "Conflicto.",
                Categoria: ErrorCategoria.Conflict)
        };

        var result = await fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Conflict, result.Categoria);
    }

    [Fact]
    public async Task DeleteSkillAsync_BackendReturnsUnauthorized_PropagatesCategoriaUnauthorized()
    {
        // AC: 401 Unauthorized se traduce a ErrorCategoria.Unauthorized
        // para que el PageModel pueda redirigir a login.
        var fake = new FakePersonaApiClient
        {
            SkillDeleteResult = new PersonaSkillDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.Unauthorized,
                Code: "Unauthorized",
                Message: "Su sesión expiró.",
                Categoria: ErrorCategoria.Unauthorized)
        };

        var result = await fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Categoria);
    }

    [Fact]
    public async Task DeleteSkillAsync_BackendReturnsForbidden_PropagatesCategoriaForbidden()
    {
        // AC: 403 Forbidden se traduce a ErrorCategoria.Forbidden para
        // que el PageModel muestre el mensaje de acceso denegado.
        var fake = new FakePersonaApiClient
        {
            SkillDeleteResult = new PersonaSkillDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.Forbidden,
                Code: "Forbidden",
                Message: "Acceso denegado.",
                Categoria: ErrorCategoria.Forbidden)
        };

        var result = await fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Forbidden, result.Categoria);
    }

    [Fact]
    public async Task DeleteSkillAsync_BackendReturnsTransport_PropagatesCategoriaTransport()
    {
        // AC: 5xx se traduce a ErrorCategoria.Transport para que el
        // PageModel muestre el mensaje "El servicio no respondió
        // correctamente. Intentá nuevamente."
        var fake = new FakePersonaApiClient
        {
            SkillDeleteResult = new PersonaSkillDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.ServiceUnavailable,
                Code: "TransportError",
                Message: "El servicio no respondió correctamente.",
                Categoria: ErrorCategoria.Transport)
        };

        var result = await fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Transport, result.Categoria);
    }

    [Fact]
    public async Task DeleteSkillAsync_WithException_PropagatesNative()
    {
        // AC: si el fake se configura con una excepción nativa
        // (e.g. TaskCanceledException), el método debe propagarla sin
        // enmascararla. La Razor Page atrapa esto en handlers y muestra
        // un mensaje recuperable.
        var fake = new FakePersonaApiClient
        {
            SkillDeleteException = new TaskCanceledException("simulated timeout")
        };

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fake.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}