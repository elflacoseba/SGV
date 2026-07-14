using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class HealthTests
{
    private readonly WebIntegrationFixture _fixture;

    public HealthTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Live_AnonymousReturns200()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_UpstreamHealthy_Returns200()
    {
        using var handler = new StubHealthProbeHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                services.AddHttpClient("SgvApiHealthProbe")
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/health/ready");
        await factory.DisposeAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_UpstreamDown_Returns503()
    {
        using var handler = new StubHealthProbeHandler(_ => throw new HttpRequestException("Connection refused"));
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                services.AddHttpClient("SgvApiHealthProbe")
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/health/ready");
        await factory.DisposeAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_UpstreamSlow_Returns503()
    {
        using var handler = new StubTimingOutHandler();
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                services.AddHttpClient("SgvApiHealthProbe")
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/health/ready");
        await factory.DisposeAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Ready_NoCookie_NoRedirect(string path)
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync(path);

        // Health endpoints must NOT redirect to sign-in even without auth
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Found, response.StatusCode);
        Assert.NotEqual(302, (int)response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// HttpMessageHandler stub for health probe tests that returns a fixed response,
/// throws on request, or blocks until cancelled (simulating timeout).
/// </summary>
internal sealed class StubHealthProbeHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _fixedResponse;
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _syncFactory;

    public StubHealthProbeHandler(HttpResponseMessage response)
    {
        _fixedResponse = response;
    }

    public StubHealthProbeHandler(Func<HttpRequestMessage, HttpResponseMessage> syncFactory)
    {
        _syncFactory = syncFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_syncFactory is not null)
        {
            return Task.FromResult(_syncFactory(request));
        }

        if (_fixedResponse is not null)
        {
            return Task.FromResult(_fixedResponse);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}

/// <summary>
/// Handler that delays until the cancellation token fires, simulating a slow
/// upstream that causes HttpClient.Timeout. The health check should catch
/// TaskCanceledException and return Unhealthy (503).
/// </summary>
internal sealed class StubTimingOutHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Wait until the HttpClient.Timeout cancels the linked token (3s).
        // This throws OperationCanceledException which HttpClient converts to
        // TaskCanceledException, matching the health check's catch block.
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
