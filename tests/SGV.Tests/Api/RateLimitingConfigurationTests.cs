using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Smoke test for the rate limit configuration in
/// <c>SGV.Api/Program.cs</c>: the
/// <see cref="RateLimiterOptions.RejectionStatusCode"/> MUST be
/// <c>429 Too Many Requests</c> so any named policy that rejects
/// (forgot-password or reset-password) responds with the canonical
/// HTTP status. Per-policy limits (3/15min and 5/15min) are pinned by
/// the endpoint-level integration tests in
/// <see cref="AuthControllerPasswordResetTests"/>.
/// </summary>
public sealed class RateLimitingConfigurationTests
{
    [Fact]
    public void Api_HostConfiguresRejectionStatusCode_429()
    {
        using var factory = new SGV.Tests.Api.Collections.ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
    }
}
