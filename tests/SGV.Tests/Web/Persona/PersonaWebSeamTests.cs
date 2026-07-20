using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests del seam entre la web shell y el fake/client de Personas para
/// PR 4/4. Cubre la registración del <see cref="IPersonaApiClient"/> en
/// el contenedor de DI, el override vía
/// <see cref="SgvWebApplicationFactory.WithPersonaApiClient"/>, el helper
/// <see cref="WebIntegrationFixture.CreatePersonaLeaseAsync"/>, y la
/// integridad observable del <see cref="FakePersonaApiClient"/> ante los
/// escenarios que usan las pages (success, 404, 409, transport failure).
/// Espejo de <c>CargoWebSeamTests</c>.
/// </summary>
[Collection("WebIntegration")]
public class PersonaWebSeamTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaWebSeamTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: shape del viewmodel de grilla y record shape
    // ──────────────────────────────────────────────

    [Fact]
    public void PersonaListItemViewModel_Constructor_ExposesAllProperties()
    {
        var id = Guid.NewGuid();
        var vm = new PersonaListItemViewModel(
            id, "L-001", "Ana", "García", "ana@example.com", "DNI", "30123456", "+5491112345678", true);

        Assert.Equal(id, vm.Id);
        Assert.Equal("L-001", vm.Legajo);
        Assert.Equal("Ana", vm.Nombres);
        Assert.Equal("García", vm.Apellidos);
        Assert.Equal("ana@example.com", vm.Email);
        Assert.Equal("DNI", vm.TipoDocumento);
        Assert.Equal("30123456", vm.NumeroDocumento);
        Assert.Equal("+5491112345678", vm.Telefono);
        Assert.True(vm.Activa);
    }

    [Fact]
    public void PersonaListQueryViewModel_Constructor_ExposesAllProperties()
    {
        var vm = new PersonaListQueryViewModel(Status: "eliminadas", Search: "garcia", Sort: "apellidos_desc", Page: 3);

        Assert.Equal("eliminadas", vm.Status);
        Assert.Equal("garcia", vm.Search);
        Assert.Equal("apellidos_desc", vm.Sort);
        Assert.Equal(3, vm.Page);
    }

    [Fact]
    public void PersonaDeleteResult_Constructor_ExposesAllProperties()
    {
        var result = new PersonaDeleteResult(
            Succeeded: true,
            StatusCode: HttpStatusCode.NoContent,
            Code: "Code",
            Message: "Message",
            Categoria: SGV.Contracts.Comun.ErrorCategoria.NotFound);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal("Code", result.Code);
        Assert.Equal("Message", result.Message);
        Assert.Equal(SGV.Contracts.Comun.ErrorCategoria.NotFound, result.Categoria);
    }

    [Fact]
    public void PersonaCommandResult_Success_HasNoErrorAndNoFieldErrors()
    {
        var dto = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var result = PersonaCommandResult.Success(dto);

        Assert.True(result.IsSuccess);
        Assert.Same(dto, result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public void PersonaCommandResult_Failure_WithErrorOnly_ProducesFailureWithoutFieldErrors()
    {
        var error = new PersonaError(PersonaErrorType.NotFound, "NotFound", "No existe");
        var result = PersonaCommandResult.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Same(error, result.Error);
        Assert.Null(result.FieldErrors);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: registración DI en Program.cs
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProductionRegistration_ResolvesPersonaApiClient()
    {
        // AC: Program.cs registra IPersonaApiClient con AddHttpClient. La
        // registración es composicional con el ApiBearerTokenHandler. Si
        // alguien la elimina por error, este test lo detecta antes de que
        // las Razor Pages fallen en runtime.
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();
        using var scope = lease.Factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IPersonaApiClient>();

        Assert.NotNull(client);
        Assert.IsType<PersonaApiClient>(client);
    }

    // ──────────────────────────────────────────────
    // T-XX 7: override del fake vía WithOverrides
    // ──────────────────────────────────────────────

    [Fact]
    public async Task WithOverrides_PersonaApiClient_SwapsToFakeImplementation()
    {
        var fake = new FakePersonaApiClient
        {
            DeleteResult = new PersonaDeleteResult(
                Succeeded: true,
                StatusCode: HttpStatusCode.NoContent,
                Code: null,
                Message: null)
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(fake);
        using var scope = lease.Factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IPersonaApiClient>();

        Assert.Same(fake, resolved);
    }

    // ──────────────────────────────────────────────
    // T-XX 8: comportamiento default del fake
    // ──────────────────────────────────────────────

    [Fact]
    public async Task WithOverrides_PersonaApiClient_DefaultDesactivarAsync_ReturnsSuccess()
    {
        // AC: por defecto, el fake devuelve éxito con 204 No Content. Si
        // alguien cambia el default sin actualizar las pruebas, este test
        // falla ruidosamente en vez de propagar el cambio silencioso.
        var fake = new FakePersonaApiClient();
        var id = Guid.NewGuid();

        var result = await fake.DesactivarAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Contains(id, fake.DeleteCalls);
    }

    [Fact]
    public async Task WithOverrides_PersonaApiClient_ConfiguredDeleteResult_IsReturned()
    {
        var fake = new FakePersonaApiClient
        {
            DeleteResult = new PersonaDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.Conflict,
                Code: "LegajoDuplicado",
                Message: "Ya existe una persona activa con el legajo L-DUP.",
                Categoria: SGV.Contracts.Comun.ErrorCategoria.Conflict)
        };
        var id = Guid.NewGuid();

        var result = await fake.DesactivarAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("LegajoDuplicado", result.Code);
        Assert.Equal("Ya existe una persona activa con el legajo L-DUP.", result.Message);
        Assert.Equal(SGV.Contracts.Comun.ErrorCategoria.Conflict, result.Categoria);
        Assert.Contains(id, fake.DeleteCalls);
    }

    [Fact]
    public async Task FakePersonaApiClient_CreateWithTransportFailure_PropagatesNativeException()
    {
        // web-apiclient-transport-contract: el fake respeta el Exception
        // configurado (HttpRequestException / TaskCanceledException) sin
        // convertirlo a CommandResult.Transport. Los PageModels dependen
        // de esta semántica para ramificar vía TransportFailureClassifier.
        var fake = new FakePersonaApiClient
        {
            CreateException = new HttpRequestException("network down")
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => fake.CreateAsync(new CrearPersonaRequest("L-001", "Ana", "García")));
    }

    [Fact]
    public async Task FakePersonaApiClient_UpdateWithTransportFailure_PropagatesNativeException()
    {
        var fake = new FakePersonaApiClient
        {
            UpdateException = new TaskCanceledException("request canceled")
        };

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fake.UpdateAsync(Guid.NewGuid(), new ActualizarPersonaRequest("L-001", "Ana", "García")));
    }
}