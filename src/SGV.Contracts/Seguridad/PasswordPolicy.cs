namespace SGV.Contracts.Seguridad;

/// <summary>
/// Single source of truth for the password policy enforced across
/// <c>SGV.Api</c> (Identity <c>PasswordOptions</c> and FluentValidation
/// request validators) and <c>SGV.Web</c> (Razor Page pre-flight checks).
/// </summary>
/// <remarks>
/// <para>
/// Before this class existed the policy was duplicated in five places:
/// <c>src/SGV.Api/Program.cs</c> (<c>AddIdentityCore</c>),
/// <c>src/SGV.Aplicacion/Seguridad/PasswordChange/ChangePasswordRequestValidator.cs</c>,
/// <c>src/SGV.Aplicacion/Seguridad/PasswordReset/ResetPasswordRequestValidator.cs</c>,
/// <c>src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.MeetsPasswordPolicy</c>, and
/// <c>src/SGV.Web/Pages/Auth/ResetPassword.cshtml.MeetsPasswordPolicy</c>.
/// A change to <see cref="MinLength"/> required editing five files without
/// any compiler help; if one was missed, signup and recovery paths drifted
/// silently.
/// </para>
/// <para>
/// <see cref="IsCompliant"/> is the client-side mirror used by Razor Pages
/// to short-circuit obviously-invalid submissions without a round-trip to
/// the API. The API re-validates against the FluentValidator that consumes
/// the same constants, so a bypass of the client check still surfaces at
/// the server with the standard 400 <c>ValidationProblemDetails</c>.
/// </para>
/// </remarks>
public static class PasswordPolicy
{
    /// <summary>Minimum password length enforced by Identity and validators.</summary>
    public const int MinLength = 6;

    /// <summary>Whether the password MUST contain at least one lowercase letter.</summary>
    public const bool RequireLowercase = true;

    /// <summary>Whether the password MUST contain at least one uppercase letter.</summary>
    public const bool RequireUppercase = true;

    /// <summary>Whether the password MUST contain at least one digit.</summary>
    public const bool RequireDigit = true;

    /// <summary>Whether the password MUST contain at least one non-alphanumeric symbol.</summary>
    public const bool RequireNonAlphanumeric = true;

    /// <summary>Regex pattern matching one or more lowercase ASCII letters.</summary>
    public const string LowercasePattern = "[a-z]+";

    /// <summary>Regex pattern matching one or more uppercase ASCII letters.</summary>
    public const string UppercasePattern = "[A-Z]+";

    /// <summary>Regex pattern matching one or more ASCII digits.</summary>
    public const string DigitPattern = "[0-9]+";

    /// <summary>Regex pattern matching one or more non-alphanumeric characters.</summary>
    public const string NonAlphanumericPattern = "[^a-zA-Z0-9]+";

    /// <summary>
    /// Validates <paramref name="password"/> against the policy. Returns
    /// <see langword="false"/> for null/empty/whitespace input so the
    /// caller does not have to short-circuit separately.
    /// </summary>
    public static bool IsCompliant(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return false;
        }

        if (RequireLowercase && !System.Text.RegularExpressions.Regex.IsMatch(password, LowercasePattern))
        {
            return false;
        }

        if (RequireUppercase && !System.Text.RegularExpressions.Regex.IsMatch(password, UppercasePattern))
        {
            return false;
        }

        if (RequireDigit && !System.Text.RegularExpressions.Regex.IsMatch(password, DigitPattern))
        {
            return false;
        }

        if (RequireNonAlphanumeric && !System.Text.RegularExpressions.Regex.IsMatch(password, NonAlphanumericPattern))
        {
            return false;
        }

        return true;
    }
}
