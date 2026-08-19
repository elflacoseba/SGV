using FluentValidation;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordReset;

/// <summary>
/// Validates <see cref="ResetPasswordRequest"/>. The required-shape
/// checks mirror <see cref="ForgotPasswordRequestValidator"/>; the
/// password rule is sourced from <see cref="PasswordPolicy"/> (the same
/// constants consumed by <c>SGV.Api/Program.cs</c> for
/// <c>IdentityOptions.Password</c>) so a user that recovered their
/// account cannot keep a password the signup path would have rejected.
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.UserId)
            .NotEmpty()
            .WithMessage("El identificador de usuario es obligatorio.");

        RuleFor(r => r.Token)
            .NotEmpty()
            .WithMessage("El token de recuperación es obligatorio.");

        RuleFor(r => r.NewPassword)
            .NotEmpty()
            .WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(PasswordPolicy.MinLength)
            .WithMessage($"La contraseña debe tener al menos {PasswordPolicy.MinLength} caracteres.")
            .Matches(PasswordPolicy.LowercasePattern)
            .WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches(PasswordPolicy.UppercasePattern)
            .WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches(PasswordPolicy.DigitPattern)
            .WithMessage("La contraseña debe incluir al menos un dígito.")
            .Matches(PasswordPolicy.NonAlphanumericPattern)
            .WithMessage("La contraseña debe incluir al menos un símbolo (no alfanumérico).");
    }
}
