using FluentValidation;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordReset;

/// <summary>
/// Validates <see cref="ValidateResetTokenRequest"/>. Only checks that
/// both <c>UserId</c> and <c>Token</c> are present; the actual token
/// cryptographic validation happens in
/// <see cref="IPasswordResetService.ValidateResetTokenAsync"/>.
/// </summary>
public sealed class ValidateResetTokenRequestValidator : AbstractValidator<ValidateResetTokenRequest>
{
    public ValidateResetTokenRequestValidator()
    {
        RuleFor(r => r.UserId)
            .NotEmpty()
            .WithMessage("El identificador de usuario es obligatorio.");

        RuleFor(r => r.Token)
            .NotEmpty()
            .WithMessage("El token de recuperación es obligatorio.");
    }
}
