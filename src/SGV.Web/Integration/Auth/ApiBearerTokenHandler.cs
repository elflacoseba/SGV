using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Bridges the JWT bearer token stored on the SGV.Web authentication ticket
/// into the <c>Authorization: Bearer ...</c> header on every outgoing HTTP
/// request sent by typed <see cref="System.Net.Http.HttpClient"/>s targeting
/// SGV.Api.
/// </summary>
/// <remarks>
/// <para>
/// The SGV.Web shell signs users in with cookie auth and stores the API-issued
/// JWT on the cookie ticket via
/// <see cref="AuthenticationTokenExtensions.StoreTokens"/> under the name
/// <see cref="AuthTokenNames.AccessToken"/>. SGV.Api, however, validates only
/// <c>Authorization: Bearer ...</c> headers (see
/// <c>src/SGV.Api/Program.cs</c>). Without this handler, every typed-client
/// call would arrive at the API as anonymous and be rejected by its
/// <c>[Authorize]</c> guard.
/// </para>
/// <para>Behavior:
/// <list type="bullet">
///   <item>If <see cref="IHttpContextAccessor.HttpContext"/> is <c>null</c>
///   (background work, startup, host lifetime callbacks) the request is
///   forwarded unchanged.</item>
///   <item>If no <c>access_token</c> is present on the inbound context's
///   authentication ticket, the request is forwarded unchanged.</item>
///   <item>If the outgoing request already carries an <c>Authorization</c>
///   header set by a caller (e.g. service-to-service, background jobs),
///   the handler defers to it instead of clobbering.</item>
///   <item>Otherwise the handler reads the stored token via
///   <see cref="HttpContext.GetTokenAsync(string)"/> and sets
///   <c>Authorization: Bearer &lt;token&gt;</c> on the outgoing request.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ApiBearerTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<ApiBearerTokenHandler> logger) : DelegatingHandler
{
    private const string BearerScheme = "Bearer";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await TryAttachBearerAsync(request).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryAttachBearerAsync(HttpRequestMessage request)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            logger.LogDebug("No HttpContext available; forwarding request without bearer header.");
            return;
        }

        if (request.Headers.Authorization is not null)
        {
            logger.LogDebug("Outgoing request already carries an Authorization header; deferring to caller.");
            return;
        }

        if (httpContext.RequestServices is null)
        {
            logger.LogDebug("HttpContext has no RequestServices; cannot resolve token; forwarding without bearer header.");
            return;
        }

        var token = await httpContext.GetTokenAsync(AuthTokenNames.AccessToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                logger.LogWarning(
                    "Authenticated request to {Path} has no {TokenName} on the cookie ticket; API will reject with 401.",
                    request.RequestUri?.PathAndQuery,
                    AuthTokenNames.AccessToken);
            }
            else
            {
                logger.LogDebug("No authenticated user; forwarding request without bearer header.");
            }

            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
    }
}