using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Typed HTTP client for SGV authentication endpoints.
/// </summary>
public sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(AuthApiRoutes.Login, request, cancellationToken);

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
        => throw new NotImplementedException("Password recovery is implemented in WU-15.");

    /// <inheritdoc />
    public Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Password recovery is implemented in WU-15.");
}
