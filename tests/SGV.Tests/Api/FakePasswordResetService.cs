using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Tests.Api;

/// <summary>
/// In-process stand-in for <see cref="IPasswordResetService"/> used
/// by <c>AuthControllerPasswordResetTests</c>. The fake answers
/// deterministically per token so the controller mapping (Success /
/// InvalidToken) can be asserted directly without Identity involvement.
/// </summary>
internal sealed class FakePasswordResetService : IPasswordResetService
{
    public Task<PasswordResetOutcome> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PasswordResetOutcome.Success);

    public Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            string.Equals(request.Token, "valid", StringComparison.Ordinal)
                ? PasswordResetOutcome.Success
                : PasswordResetOutcome.InvalidToken);
}
