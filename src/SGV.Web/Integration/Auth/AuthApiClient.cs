using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Typed HTTP client for SGV authentication endpoints.
/// </summary>
public sealed class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient httpClient;
    private readonly HttpClient anonymousHttpClient;

    /// <summary>
    /// Creates an authentication client with separate authenticated and anonymous transports.
    /// </summary>
    /// <param name="httpClient">Transport for authenticated authentication operations.</param>
    /// <param name="anonymousHttpClient">Transport for anonymous password recovery operations.</param>
    internal AuthApiClient(HttpClient httpClient, HttpClient anonymousHttpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.anonymousHttpClient = anonymousHttpClient ?? throw new ArgumentNullException(nameof(anonymousHttpClient));
    }

    /// <summary>
    /// Creates an authentication client using one transport for backwards-compatible test overrides.
    /// Anonymous transport registration is composed in the Web composition root.
    /// </summary>
    /// <param name="httpClient">Transport for authenticated operations.</param>
    public AuthApiClient(HttpClient httpClient)
        : this(httpClient, httpClient)
    {
    }

    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            AuthApiRoutes.Login,
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<PasswordResetOutcome> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
        => PostAnonymousAsync(AuthApiRoutes.ForgotPassword, request, cancellationToken);

    /// <inheritdoc />
    public Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
        => PostAnonymousAsync(AuthApiRoutes.ResetPassword, request, cancellationToken);

    private async Task<PasswordResetOutcome> PostAnonymousAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await anonymousHttpClient.PostAsJsonAsync(
            route,
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return PasswordResetOutcome.Success;
    }
}
