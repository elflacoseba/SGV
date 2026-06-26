using SGV.Aplicacion.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Authentication client abstraction used by SGV.Web.
/// </summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Attempts to authenticate a user against SGV.Api.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login response when authentication succeeds; otherwise null.</returns>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
