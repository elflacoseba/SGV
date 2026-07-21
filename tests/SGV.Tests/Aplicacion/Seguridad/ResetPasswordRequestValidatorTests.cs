using FluentValidation.TestHelper;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

/// <summary>
/// Validates <see cref="ResetPasswordRequest"/>: <c>UserId</c> and
/// <c>Token</c> must be present (the controller already rejects empty
/// bodies), and <see cref="ResetPasswordRequest.NewPassword"/> MUST
/// satisfy the same <c>IdentityOptions.Password</c> policy enforced at
/// signup so a user who recovered their account cannot use a password
/// that the signup path would have rejected.
/// </summary>
public sealed class ResetPasswordRequestValidatorTests
{
    private static ResetPasswordRequest RequestValido(string newPassword = "Password1!") =>
        new(UserId: "user-1", Token: "CfDJ8rawtoken", NewPassword: newPassword);

    private readonly ResetPasswordRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_UserId_Is_Empty(string userId)
    {
        var request = new ResetPasswordRequest(userId, "token", "Password1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Token_Is_Empty(string token)
    {
        var request = new ResetPasswordRequest("user-1", token, "Password1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_NewPassword_Is_Empty(string newPassword)
    {
        var request = new ResetPasswordRequest("user-1", "token", newPassword);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.NewPassword);
    }

    [Theory]
    [InlineData("short1!")]                 // 7 chars passes length but shorter than 6 is fine; 6 is the minimum
    [InlineData("nouppercase1!")]            // missing upper
    [InlineData("NOLOWERCASE1!")]            // missing lower
    [InlineData("NoDigits!!")]               // missing digit
    [InlineData("NoSymbol123")]              // missing non-alphanumeric
    public void Should_Have_Error_When_NewPassword_FailsIdentityPolicy(string newPassword)
    {
        var request = new ResetPasswordRequest("user-1", "token", newPassword);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.NewPassword);
    }

    [Fact]
    public void Should_Have_Error_When_NewPassword_Is_Shorter_Than_Six()
    {
        var request = new ResetPasswordRequest("user-1", "token", "Ab1!");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.NewPassword);
    }

    [Fact]
    public void Should_Not_Have_Error_When_NewPassword_SatisfiesPolicy()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.NewPassword);
    }

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
