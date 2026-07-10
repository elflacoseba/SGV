using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// End-to-end tests for the JWT signing path: tokens issued by
/// <see cref="SGV.Infraestructura.Seguridad.AuthServicio"/> using the configured
/// signing key MUST be accepted by the JwtBearer middleware, and tokens
/// signed with a different key MUST be rejected with 401.
///
/// These tests exercise the real signing/validation chain through HTTP,
/// contrary to <see cref="SGV.Tests.Api.AuthControllerTests"/> which uses the
/// fake auth scheme registered by <see cref="SGV.Tests.Api.ApiWebApplicationFactory"/>.
/// </summary>
public sealed class JwtRealAuthTests
{
    private const string LoginRelative = "api/v1/auth/login";
    private const string UsuariosRelative = "api/v1/usuarios";

    private static class TestKeys
    {
        public const string Host = "TEST-KEY-HOST-MIN-32-BYTES-PADDING!!"; // 36 chars
        public const string Foreign = "TEST-KEY-FOREIGN-32-BYTES-PADDING!!"; // 37 chars
    }

    [MySqlFact]
    public async Task TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200()
    {
        using var factory = new JwtRealWebApplicationFactory(signingKey: TestKeys.Host);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(LoginRelative,
            new LoginRequest("admin", "Admin#12345"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, UsuariosRelative);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        var protectedResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [MySqlFact]
    public async Task TokenFirmado_ConClaveDistinta_Rechazado_401()
    {
        using var factory = new JwtRealWebApplicationFactory(signingKey: TestKeys.Host);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        // Firmed HS256 with a key that is NOT the one configured in the factory.
        var foreign = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "SGV",
            audience: "SGV",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "attacker")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKeys.Foreign)),
                SecurityAlgorithms.HmacSha256)));

        using var request = new HttpRequestMessage(HttpMethod.Get, UsuariosRelative);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", foreign);

        var protectedResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }
}
