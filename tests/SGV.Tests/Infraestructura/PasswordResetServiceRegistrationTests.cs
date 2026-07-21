using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Verifies that the Infrastructure composition root registers the
/// <see cref="IPasswordResetService"/> port and binds it to the
/// concrete <c>PasswordResetService</c> implementation. The DI graph
/// MUST also make the dependencies of the service (Identity's
/// <see cref="UserManager{TUser}"/>, the configured
/// <see cref="IEmailSender{TUser}"/>) reachable from a single scope.
/// </summary>
public sealed class PasswordResetServiceRegistrationTests
{
    [Fact]
    public void Api_HostResolvesIPasswordResetService_AsPasswordResetService()
    {
        using var factory = new SGV.Tests.Api.Collections.ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetService<IPasswordResetService>();

        Assert.NotNull(service);
        Assert.IsType<PasswordResetService>(service);
    }

    [Fact]
    public void Api_HostResolvesIPasswordResetServiceAsScoped_NotSingleton()
    {
        // The service depends on UserManager<T> (Scoped by Identity
        // composition). It cannot be Singleton; the test pins the
        // lifetime contract.
        using var factory = new SGV.Tests.Api.Collections.ApiIntegrationFixture().RootFactory;
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var first = scope1.ServiceProvider.GetRequiredService<IPasswordResetService>();
        var second = scope2.ServiceProvider.GetRequiredService<IPasswordResetService>();

        Assert.NotSame(first, second);
    }
}
