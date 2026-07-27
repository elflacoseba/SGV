using FluentValidation.TestHelper;
using SGV.Aplicacion.Seguridad.PasswordChange;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

public sealed class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Theory]
    [InlineData("Current1!", "NewPassword1!", "NewPassword1!", true)]
    [InlineData("", "NewPassword1!", "NewPassword1!", false)]
    [InlineData("Current1!", "Ab1!", "Ab1!", false)]
    [InlineData("Current1!", "newpassword1!", "newpassword1!", false)]
    [InlineData("Current1!", "NewPassword1!", "Different1!", false)]
    public void Validate_VariousRequests_ReturnsExpectedValidity(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        bool expectedValid)
    {
        var request = new ChangePasswordRequest(currentPassword, newPassword, confirmPassword);

        var result = _validator.TestValidate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }
}
