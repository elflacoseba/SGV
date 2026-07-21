using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Smoke tests that lock in the public wire surface for the password reset
/// flow (issue #181). The constants MUST be stable across API and Web so
/// the typed <see cref="HttpClient"/>s on the Web side keep linking against
/// the right endpoints.
/// </summary>
public sealed class PasswordResetContractsTests
{
    [Fact]
    public void AuthApiRoutes_ForgotPasswordRelativeAndAbsolute_AreStable()
    {
        Assert.Equal("forgot-password", AuthApiRoutes.ForgotPasswordRelative);
        Assert.Equal("/api/v1/auth/forgot-password", AuthApiRoutes.ForgotPassword);
    }

    [Fact]
    public void AuthApiRoutes_ResetPasswordRelativeAndAbsolute_AreStable()
    {
        Assert.Equal("reset-password", AuthApiRoutes.ResetPasswordRelative);
        Assert.Equal("/api/v1/auth/reset-password", AuthApiRoutes.ResetPassword);
    }

    [Fact]
    public void ForgotPasswordRequest_StoresUserNameOrEmailAsSoleMember()
    {
        var request = new ForgotPasswordRequest("user@example.com");

        Assert.Equal("user@example.com", request.UserNameOrEmail);
    }

    [Fact]
    public void ResetPasswordRequest_StoresUserIdTokenAndNewPasswordAtomically()
    {
        var request = new ResetPasswordRequest(
            UserId: "user-1",
            Token: "CfDJ8abc",
            NewPassword: "Password1!");

        Assert.Equal("user-1", request.UserId);
        Assert.Equal("CfDJ8abc", request.Token);
        Assert.Equal("Password1!", request.NewPassword);
    }
}
