using Microsoft.AspNetCore.Identity;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Strict-TDD coverage for <see cref="UsuarioIdentityGateway.ToIdentityFailure"/>:
/// every <c>IdentityError.Code</c> reachable under the current
/// <c>IdentityOptions.Password</c> policy plus the format/duplication codes
/// already covered by the gateway must be translated to Spanish before
/// reaching the client. Codes not in the map must fall back to a generic
/// Spanish message — never English.
/// <para>
/// Scope: <c>identity-user-role-management</c> delta spec, change
/// <c>2026-07-18-fix-170-crear-usuario-roles-identity</c>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.AspNetCore.Identity.IdentityError</c> in
/// <c>Microsoft.Extensions.Identity.Core 9.0.0</c> exposes only
/// <c>Code</c> and <c>Description</c>; the <c>Metadata</c> property
/// that the design explored is not present in this version. The
/// gateway therefore uses a hardcoded Spanish message keyed on the
/// <c>Code</c>. The numbers in <see cref="PasswordTooShort"/> and
/// <see cref="PasswordRequiresUniqueChars"/> mirror the configuration
/// in <c>SGV.Api/Program.cs</c> (<c>RequiredLength = 6</c>,
/// <c>RequireUniqueChars</c> default = 1) and the canonical Spanish
/// strings mandated by the spec scenarios.
/// </para>
/// </remarks>
public sealed class UsuarioIdentityGatewayToIdentityFailureTests
{
    [Theory]
    [InlineData("PasswordTooShort", "al menos 6 caracteres")]
    [InlineData("PasswordRequiresNonAlphanumeric", "al menos un carácter no alfanumérico")]
    [InlineData("PasswordRequiresDigit", "al menos un dígito")]
    [InlineData("PasswordRequiresLower", "al menos una letra minúscula")]
    [InlineData("PasswordRequiresUpper", "al menos una letra mayúscula")]
    [InlineData("PasswordRequiresUniqueChars", "al menos 1 carácter único")]
    [InlineData("DuplicateUserName", "nombre de usuario ya está en uso")]
    [InlineData("DuplicateEmail", "email ya está en uso")]
    [InlineData("InvalidEmail", "email no tiene un formato válido")]
    [InlineData("InvalidUserName", "letras, números, punto, guión bajo y guión medio")]
    public void ToIdentityFailure_LocalizaCodigoConocido(string code, string expectedFragment)
    {
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = code, Description = "English description that must be ignored." });

        var result = UsuarioIdentityGateway.ToIdentityFailure(identityResult);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains(expectedFragment, result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToIdentityFailure_CodigoNoMapeado_CaeAFallbackEnEspanol()
    {
        // ConcurrencyFailure is a valid Identity error code not handled by
        // the gateway's explicit map — must still surface in Spanish.
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = "ConcurrencyFailure", Description = "Optimistic concurrency failure." });

        var result = UsuarioIdentityGateway.ToIdentityFailure(identityResult);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(UsuarioErrorType.Validation, result.Error!.Type);
        // English fallback text must NOT appear.
        Assert.DoesNotContain("concurrency", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("optimistic", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        // Spanish markers — at least one accented marker / known word.
        Assert.Matches("Verifique|no se pudo|operaci[oó]n", result.Error.Message);
    }

    [Fact]
    public void ToIdentityFailure_TodosLosErroresLocalizados_CompartenCategoriaValidationYCodeIdentityError()
    {
        var codes = new[]
        {
            "PasswordTooShort",
            "PasswordRequiresNonAlphanumeric",
            "PasswordRequiresDigit",
            "PasswordRequiresLower",
            "PasswordRequiresUpper",
            "PasswordRequiresUniqueChars",
            "DuplicateUserName",
            "DuplicateEmail",
            "InvalidEmail",
            "InvalidUserName",
            "ConcurrencyFailure"
        };

        foreach (var code in codes)
        {
            var identityResult = IdentityResult.Failed(
                new IdentityError { Code = code, Description = "English description" });

            var result = UsuarioIdentityGateway.ToIdentityFailure(identityResult);

            Assert.False(result.IsSuccess, $"Code {code} should produce a failure.");
            Assert.NotNull(result.Error);
            // DuplicateUserName/DuplicateEmail remain Conflict per the
            // existing gateway contract (out of scope for #170). All other
            // codes collapse into Validation + Code="IdentityError".
            if (code is "DuplicateUserName" or "DuplicateEmail")
            {
                Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
                Assert.NotEqual("IdentityError", result.Error.Code);
            }
            else
            {
                Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
                Assert.Equal("IdentityError", result.Error.Code);
            }
        }
    }
}
