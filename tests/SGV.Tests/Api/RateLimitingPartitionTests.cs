using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// C-1 release-readiness: las 4 named policies del <c>AddRateLimiter</c>
/// ahora usan <c>RateLimitPartition.GetFixedWindowLimiter</c> con
/// partition key por IP (anonymous) o subject (authenticated). Antes
/// usaban <c>AddFixedWindowLimiter</c> sin partitioner — todas las
/// requests del proceso compartían el mismo contador y un atacante con
/// IP rotativa podía agotar la quota de usuarios legítimos.
/// </summary>
/// <remarks>
/// Los helpers de partition key (<c>PartitionKeyByIp</c> /
/// <c>PartitionKeyBySubjectOrIp</c>) son funciones locales estáticas
/// dentro de <c>Program.cs</c> (top-level statements), no son
/// accesibles públicamente. La verificación del comportamiento de
/// particionado se hace por code review del bloque
/// <c>AddRateLimiter</c>; este test solo bloquea el contrato observable:
/// <see cref="RateLimiterOptions.RejectionStatusCode"/> es 429.
/// </remarks>
[Collection("ApiIntegration")]
public sealed class RateLimitingPartitionTests
{
    [Fact]
    public void RateLimiterOptions_RejectionStatusCode_Is429()
    {
        using var factory = new ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
    }
}
