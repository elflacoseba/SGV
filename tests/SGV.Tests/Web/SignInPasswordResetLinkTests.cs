using System.Net;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class SignInPasswordResetLinkTests
{
    private readonly WebIntegrationFixture _fixture;

    public SignInPasswordResetLinkTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_SignIn_RendersForgotPasswordLinkToPublicPage()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/auth/sign-in");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/auth/forgot-password\"", content);
        Assert.Contains("¿Olvidaste tu contraseña?", content);
    }
}
