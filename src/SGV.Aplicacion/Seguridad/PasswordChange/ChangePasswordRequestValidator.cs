using FluentValidation;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordChange;

/// <summary>
/// Validates authenticated password-change requests against the Identity password policy.
/// </summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    private const int MinLength = 6;

    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithMessage("La contraseña actual es obligatoria.");

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(MinLength)
            .WithMessage($"La contraseña debe tener al menos {MinLength} caracteres.")
            .Matches("[a-z]+")
            .WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[A-Z]+")
            .WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[0-9]+")
            .WithMessage("La contraseña debe incluir al menos un dígito.")
            .Matches(@"[^a-zA-Z0-9]+")
            .WithMessage("La contraseña debe incluir al menos un símbolo (no alfanumérico).");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword, StringComparer.Ordinal)
            .WithMessage("La confirmación no coincide con la nueva contraseña.");
    }
}
