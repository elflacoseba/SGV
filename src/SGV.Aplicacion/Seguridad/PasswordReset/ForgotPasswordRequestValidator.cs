using FluentValidation;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordReset;

/// <summary>
/// Validates <see cref="ForgotPasswordRequest"/>. Only the
/// presence/emptiness of the identifier is checked: the recovery flow
/// is anti-enumeration by design, so any additional format check
/// (e.g. email shape) would leak which identifiers the backend
/// considers valid.
/// </summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(r => r.UserNameOrEmail)
            .NotEmpty()
            .WithMessage("Ingresá tu nombre de usuario o email.");
    }
}
