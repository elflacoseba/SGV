using FluentValidation;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordReset;

/// <summary>
/// Validates <see cref="ResetPasswordRequest"/>. The required-shape
/// checks mirror <see cref="ForgotPasswordRequestValidator"/>; the
/// password rule mirrors the policy set in
/// <c>SGV.Api/Program.cs</c> for <c>IdentityOptions.Password</c>
/// (<c>RequiredLength=6</c>, requires lower, upper, digit, and
/// non-alphanumeric) so a user that recovered their account cannot
/// keep a password the signup path would have rejected.
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>Minimum length enforced by Identity's password policy.</summary>
    private const int MinLength = 6;

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
            .MinimumLength(MinLength)
            .WithMessage($"La contraseña debe tener al menos {MinLength} caracteres.")
            .Matches("[a-z]+", RegexOptionsHolder.LowercaseOptions)
            .WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[A-Z]+", RegexOptionsHolder.UppercaseOptions)
            .WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[0-9]+", RegexOptionsHolder.DigitOptions)
            .WithMessage("La contraseña debe incluir al menos un dígito.")
            .Matches(@"[^a-zA-Z0-9]+", RegexOptionsHolder.SymbolOptions)
            .WithMessage("La contraseña debe incluir al menos un símbolo (no alfanumérico).");
    }

    /// <summary>
    /// Static <see cref="System.Text.RegularExpressions.RegexOptions"/>
    /// holders so <c>RuleFor(...).Matches(...)</c> receives a stable
    /// set of flags; configurable in one place if a future change ever
    /// needs culture-invariant matching.
    /// </summary>
    private static class RegexOptionsHolder
    {
        public static readonly System.Text.RegularExpressions.RegexOptions LowercaseOptions =
            System.Text.RegularExpressions.RegexOptions.None;
        public static readonly System.Text.RegularExpressions.RegexOptions UppercaseOptions =
            System.Text.RegularExpressions.RegexOptions.None;
        public static readonly System.Text.RegularExpressions.RegexOptions DigitOptions =
            System.Text.RegularExpressions.RegexOptions.None;
        public static readonly System.Text.RegularExpressions.RegexOptions SymbolOptions =
            System.Text.RegularExpressions.RegexOptions.None;
    }
}
