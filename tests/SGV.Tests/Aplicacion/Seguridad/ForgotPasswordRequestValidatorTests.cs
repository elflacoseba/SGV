using FluentValidation.TestHelper;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

/// <summary>
/// Validates shape and policy gates on
/// <see cref="ForgotPasswordRequest"/>. The recovery endpoint is a
/// privacy surface: an unknown identifier MUST look identical to a
/// known one to prevent user enumeration, so the validator MUST only
/// reject empty/whitespace identifiers — never enforce format.
/// </summary>
public sealed class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_UserNameOrEmail_Is_Empty(string identifier)
    {
        var request = new ForgotPasswordRequest(identifier);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.UserNameOrEmail);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("admin@example.com")]
    [InlineData(" +1 (555) 123-4567 ")]
    public void Should_Not_Have_Error_For_NonEmpty_Identifier(string identifier)
    {
        // Privacy note: the validator MUST NOT enforce email format
        // because that would leak which shape of identifier the
        // backend considers valid (and would let an attacker prune
        // candidates). Anything non-empty passes.
        var request = new ForgotPasswordRequest(identifier);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.UserNameOrEmail);
    }

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = new ForgotPasswordRequest("admin@example.com");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
