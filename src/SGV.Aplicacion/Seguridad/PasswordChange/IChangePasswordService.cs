using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordChange;

/// <summary>
/// Changes the password of the currently authenticated user.
/// </summary>
public interface IChangePasswordService
{
    /// <summary>
    /// Changes the password for the specified user.
    /// </summary>
    Task<ChangePasswordOutcome> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
