using System.Reflection;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

public sealed class AuthApiClientPasswordResetContractTests
{
    [Fact]
    public void IAuthApiClient_ExposesAnonymousPasswordResetOperations()
    {
        var forgot = typeof(IAuthApiClient).GetMethod(nameof(IAuthApiClient.ForgotPasswordAsync));
        var reset = typeof(IAuthApiClient).GetMethod(nameof(IAuthApiClient.ResetPasswordAsync));

        Assert.NotNull(forgot);
        Assert.NotNull(reset);
        Assert.Equal(typeof(Task<PasswordResetOutcome>), forgot!.ReturnType);
        Assert.Equal(typeof(Task<PasswordResetOutcome>), reset!.ReturnType);
        Assert.Equal(2, forgot.GetParameters().Length);
        Assert.Equal(2, reset.GetParameters().Length);
        Assert.Equal(typeof(CancellationToken), forgot.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), reset.GetParameters()[1].ParameterType);
    }
}
