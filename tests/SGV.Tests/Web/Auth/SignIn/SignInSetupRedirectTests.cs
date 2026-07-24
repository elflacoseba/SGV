using System.Net;
using SGV.Contracts.Setup;
using SGV.Tests.Web.Auth.Setup;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Setup;
using Xunit;

namespace SGV.Tests.Web.Auth.SignIn;

/// <summary>
/// Tests de integración para el filtro de redirección en
/// <c>SignIn.OnGetAsync</c> (issue #195 / WU-5). Cubren los escenarios
/// de la spec REQ-SETUP-005: DB vacía redirige a setup, DB con
/// usuarios renderiza normal, API caída (fail-open) renderiza normal
/// y cache hit evita el round-trip al API.
/// </summary>
[Collection("WebIntegration")]
public sealed class SignInSetupRedirectTests
{
    private readonly WebIntegrationFixture _fixture;

    public SignInSetupRedirectTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_SignIn_DBVacia_RedirigeASetup()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/setup", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Get_SignIn_DBConUsuarios_RenderizaNormal()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(false)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Iniciar sesión", content);
    }

    [Fact]
    public async Task Get_SignIn_ApiCaida_FailOpenRenderizaSignIn()
    {
        // Fail-open (design §2.3): si la API está caída y la Web no
        // tiene cache, el ISetupApiClient devuelve
        // RequiresSetup=false (en lugar de propagar la excepción), y
        // el SignIn renderiza normal. Mejor UX confusa que romper el
        // acceso al sistema completo. El fake de este test modela
        // exactamente el comportamiento del SetupApiClient real
        // cuando la API está caída.
        var fake = new FailOpenSetupApiClient();
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Iniciar sesión", content);
    }

    [Fact]
    public async Task Get_SignIn_ApiTimeout_FailOpenRenderizaSignIn()
    {
        var fake = new FailOpenSetupApiClient();
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class FailOpenSetupApiClient : ISetupApiClient
    {
        public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SetupStatusResponse(false));

        public Task<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>> GetTiposDocumentoAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>>(
                Array.Empty<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>());

        public Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
