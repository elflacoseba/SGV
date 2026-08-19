using FluentValidation;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordChange;

/// <summary>
/// Validates authenticated password-change requests against the Identity
/// password policy declared in <see cref="PasswordPolicy"/>.
/// </summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithMessage("La contraseña actual es obligatoria.");

        RuleFor(request => request.NewPassword)
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

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword, StringComparer.Ordinal)
            .WithMessage("La confirmación no coincide con la nueva contraseña.");
    }
}
