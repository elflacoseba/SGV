using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGV.Api.Seguridad;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

public sealed class RevalidatorCredencialesTests
{
    [Fact]
    public void Contract_ExposesSigueVigenteAsyncWithUserIdAndCancellationToken()
    {
        var method = typeof(IRevalidatorCredenciales).GetMethod(
            nameof(IRevalidatorCredenciales.SigueVigenteAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method!.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("userId", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Implementation_UsesScopeFactoryAndLoggerDependencies()
    {
        var constructor = typeof(RevalidatorCredenciales).GetConstructors()
            .Single();
        var parameters = constructor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IServiceScopeFactory), parameters[0].ParameterType);
        Assert.Equal(typeof(ILogger<RevalidatorCredenciales>), parameters[1].ParameterType);
        Assert.True(typeof(UserManager<SgvIdentityUser>).IsClass);
    }
}
