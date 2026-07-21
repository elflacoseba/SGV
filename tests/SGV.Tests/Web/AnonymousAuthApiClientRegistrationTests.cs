using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class AnonymousAuthApiClientRegistrationTests
{
    private readonly WebIntegrationFixture _fixture;

    public AnonymousAuthApiClientRegistrationTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public void ProductionRegistration_ResolvesSeparateAnonymousAuthHttpClient()
    {
        using var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
                services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"));
        using var scope = factory.Services.CreateScope();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var anonymousClient = httpClientFactory.CreateClient(AuthApiClient.AnonymousHttpClientName);
        var authClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        Assert.Equal(new Uri("https://api.test/"), anonymousClient.BaseAddress);
        Assert.NotSame(
            ExtractHttpClient(authClient, "httpClient"),
            ExtractHttpClient(authClient, "anonymousHttpClient"));
    }

    private static HttpClient ExtractHttpClient(IAuthApiClient client, string fieldName)
    {
        var field = client.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        return Assert.IsType<HttpClient>(field?.GetValue(client));
    }
}
