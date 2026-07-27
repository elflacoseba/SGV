using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Tests.Web.Auth.Setup;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class SignInPasswordChangeBannerTests
{
    private const string PasswordChangeMessage =
        "Tu contraseña se cambió correctamente. Volvé a iniciar sesión.";

    private readonly WebIntegrationFixture _fixture;

    public SignInPasswordChangeBannerTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_SignIn_WithPasswordChangeMessageTempData_RendersBanner()
    {
        await using var lease = await _fixture.CreateLeaseWithBootstrapAsync(
            factory => factory.WithOverrides(
                configureServices: services =>
                {
                    services.RemoveAll<ITempDataProvider>();
                    services.AddSingleton<ITempDataProvider>(
                        new PasswordChangeTempDataProvider(PasswordChangeMessage));
                },
                setupApiClient: new FakeSetupApiClient()),
            static _ => Task.CompletedTask);

        var response = await lease.Client.GetAsync("/auth/sign-in");
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(PasswordChangeMessage, content);
    }

    private sealed class PasswordChangeTempDataProvider(string message) : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>
            {
                ["PasswordChangeMessage"] = message
            };

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
