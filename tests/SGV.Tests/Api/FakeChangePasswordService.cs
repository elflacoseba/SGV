using SGV.Aplicacion.Seguridad.PasswordChange;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Tests.Api;

/// <summary>
/// In-process stand-in for <see cref="IChangePasswordService"/> used by
/// <c>AuthControllerChangePasswordTests</c>. The fake answers
/// deterministically per current-password equality so the controller
/// mapping (Success / InvalidCurrentPassword / ValidationError) can be
/// asserted directly without Identity involvement.
/// </summary>
internal sealed class FakeChangePasswordService : IChangePasswordService
{
    /// <summary>
    /// Optional override invoked for every request. When supplied, the
    /// fake returns its result regardless of <see cref="ChangePasswordRequest"/>
    /// contents. Used by tests that need to force a branch Identity
    /// would not reach (e.g. <c>ValidationError</c> from policy drift).
    /// </summary>
    public Func<ChangePasswordRequest, ChangePasswordOutcome>? Override { get; set; }

    public Task<ChangePasswordOutcome> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Override is not null)
        {
            return Task.FromResult(Override(request));
        }

        // Default behaviour: "valid" current password → Success;
        // anything else → InvalidCurrentPassword, mirroring the
        // production service's PasswordMismatch branch.
        return Task.FromResult(
            string.Equals(request.CurrentPassword, "valid", StringComparison.Ordinal)
                ? ChangePasswordOutcome.Success
                : ChangePasswordOutcome.InvalidCurrentPassword);
    }
}
