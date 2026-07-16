using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Auth;

/// <summary>
/// Revalidates the Identity subject carried by the web cookie through the
/// authenticated API boundary.
/// </summary>
/// <remarks>
/// <see cref="SGV.Web.Program"/> intentionally references only
/// <c>SGV.Contracts</c>; it cannot resolve the API host's scoped
/// <c>UserManager</c>. A dedicated HTTP client validates the bearer token
/// without invoking the cookie authentication handler recursively.
/// </remarks>
public sealed class CookiePrincipalRevalidator(
    IHttpClientFactory httpClientFactory,
    ILogger<CookiePrincipalRevalidator> logger)
{
    /// <summary>
    /// Name of the HTTP client used for the API-side credential lookup.
    /// </summary>
    public const string HttpClientName = "SgvCredentialRevalidation";

    /// <summary>
    /// Determines whether the cookie subject is still available through the
    /// API authentication boundary.
    /// </summary>
    /// <param name="userId">The Identity user identifier from the cookie.</param>
    /// <param name="accessToken">The bearer token stored on the cookie ticket.</param>
    /// <param name="cancellationToken">Token used to cancel the API lookup.</param>
    /// <returns><see langword="true"/> when the API accepts the subject, otherwise <see langword="false"/>.</returns>
    public async Task<bool> SigueVigenteAsync(
        string userId,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/usuarios/{Uri.EscapeDataString(userId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.NotFound)
            {
                return false;
            }

            logger.LogWarning(
                "Cookie validation received unexpected API status {StatusCode} for user {UserId}; preserving the cookie until the API is reachable.",
                (int)response.StatusCode,
                userId);
            return true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Cookie validation could not reach the API for user {UserId}; preserving the cookie until the API is reachable.",
                userId);
            return true;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Cookie validation timed out for user {UserId}; preserving the cookie until the API is reachable.",
                userId);
            return true;
        }
    }

    /// <summary>
    /// Rejects the current cookie when its Identity subject is blocked or
    /// no longer exists.
    /// </summary>
    /// <param name="context">The cookie validation context.</param>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var accessToken = context.Properties.GetTokenValue(AuthTokenNames.AccessToken);
        if (await SigueVigenteAsync(
                userId,
                accessToken,
                context.HttpContext.RequestAborted)
                .ConfigureAwait(false))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext
            .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
    }
}
