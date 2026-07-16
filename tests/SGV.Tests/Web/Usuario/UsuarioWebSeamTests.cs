using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Seam tests del módulo Usuarios para PR 2: forma de los records
/// (UsuarioListItemViewModel, UsuarioListQueryViewModel,
/// UsuarioCommandResult.Failure/Success), resolución del cliente
/// tipado <see cref="IUsuarioApiClient"/> desde la composición raíz
/// registrada en <c>Program.cs</c>, override del fake vía
/// <see cref="SgvWebApplicationFactory.WithOverrides"/>, e
/// integridad del <see cref="FakeUsuarioApiClient"/> ante los
/// escenarios default. Espejo estructural de
/// <c>PersonaWebSeamTests</c>.
/// </summary>
[Collection("WebIntegration")]
public class UsuarioWebSeamTests
{
    private readonly WebIntegrationFixture _fixture;

    public UsuarioWebSeamTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ── Shape de records ────────────────────────────────────────

    [Fact]
    public void UsuarioListItemViewModel_Constructor_ExposesAllProperties()
    {
        var personaId = Guid.NewGuid();
        var vm = new UsuarioListItemViewModel(
            Id: "u-1",
            UserName: "agarcía",
            Email: "agarcía@example.com",
            Nombres: "Ana",
            Apellidos: "García",
            Roles: new[] { "Administrador" },
            PersonaId: personaId);

        Assert.Equal("u-1", vm.Id);
        Assert.Equal("agarcía", vm.UserName);
        Assert.Equal("agarcía@example.com", vm.Email);
        Assert.Equal("Ana", vm.Nombres);
        Assert.Equal("García", vm.Apellidos);
        Assert.Equal(new[] { "Administrador" }, vm.Roles);
        Assert.Equal(personaId, vm.PersonaId);
    }

    [Fact]
    public void UsuarioListQueryViewModel_Constructor_ExposesAllProperties()
    {
        var vm = new UsuarioListQueryViewModel(
            Status: "eliminadas",
            Search: "garcia",
            Sort: "userName_desc",
            Page: 3);

        Assert.Equal("eliminadas", vm.Status);
        Assert.Equal("garcia", vm.Search);
        Assert.Equal("userName_desc", vm.Sort);
        Assert.Equal(3, vm.Page);
    }

    [Fact]
    public void UsuarioCommandResult_Success_HasNoError()
    {
        var dto = new UsuarioDto(
            "u-1", Guid.NewGuid(), "agarcía", "agarcía@example.com",
            new[] { "Administrador" });
        var result = UsuarioCommandResult.Success(dto);

        Assert.True(result.IsSuccess);
        Assert.Same(dto, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void UsuarioCommandResult_Failure_WithErrorOnly_ExposesError()
    {
        // PR2-HALL: `UsuarioCommandResult` heredado del PR1 NO expone
        // `FieldErrors`. El shape sólo tiene `Error` y los factories
        // `Success(value)` / `Failure(error)`. El test que asumía
        // `Failure(error, fieldErrors)` ya no aplica en PR 2; queda la
        // brecha a cerrar en PR 3/4 vía extensión del contrato.
        var error = new UsuarioError(UsuarioErrorType.NotFound, "NotFound", "No existe");
        var result = UsuarioCommandResult.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Same(error, result.Error);
    }

    // ── DI + override del seam ──────────────────────────────────

    [Fact]
    public void ProductionRegistration_ResolvesUsuarioApiClient()
    {
        // AC: Program.cs registra IUsuarioApiClient con AddHttpClient.
        // Si alguien la elimina por error, este test lo detecta antes
        // de que las Razor Pages fallen en runtime.
        using var scope = _fixture.RootFactory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IUsuarioApiClient>();

        Assert.NotNull(client);
        Assert.IsType<UsuarioApiClient>(client);
    }

    [Fact]
    public async Task WithOverrides_UsuarioApiClient_SwapsToFakeImplementation()
    {
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake);
        using var scope = lease.Factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IUsuarioApiClient>();

        Assert.Same(fake, resolved);
    }

    [Fact]
    public async Task FakeUsuarioApiClient_DefaultEliminarAsync_ReturnsSuccess()
    {
        // AC: por defecto, el fake devuelve éxito (Value nulo) para
        // reflejar el 204 No Content del backend. Si alguien cambia el
        // default sin actualizar las pruebas, este test falla
        // ruidosamente en vez de propagar el cambio silencioso.
        var fake = new FakeUsuarioApiClient();
        var id = "u-default";

        var result = await fake.EliminarAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(id, fake.EliminarCalls);
    }

    [Fact]
    public async Task FakeUsuarioApiClient_ConfiguredFailure_ReturnsConfiguredError()
    {
        var fake = new FakeUsuarioApiClient
        {
            EliminarResult = UsuarioCommandResult.Failure(
                new UsuarioError(
                    Type: UsuarioErrorType.Unauthorized,
                    Code: "AutoEliminacion",
                    Message: "No puede eliminar su propio usuario.",
                    StatusCode: 403,
                    Categoria: SGV.Contracts.Comun.ErrorCategoria.Forbidden))
        };
        var id = "u-self";

        var result = await fake.EliminarAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SGV.Contracts.Comun.ErrorCategoria.Forbidden, result.Error!.Categoria);
        Assert.Equal("AutoEliminacion", result.Error.Code);
        Assert.Contains(id, fake.EliminarCalls);
    }

    [Fact]
    public async Task FakeUsuarioApiClient_CreateWithTransportFailure_PropagatesNativeException()
    {
        // web-apiclient-transport-contract: el fake respeta el
        // Exception configurado (HttpRequestException /
        // TaskCanceledException) sin convertirlo a CommandResult.
        var fake = new FakeUsuarioApiClient
        {
            CreateException = new HttpRequestException("network down")
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => fake.CreateAsync(new CrearUsuarioRequest(
                Guid.NewGuid(), "u", "u@example.com", "Pwd!12345", new[] { "Consultor" })));
    }
}
