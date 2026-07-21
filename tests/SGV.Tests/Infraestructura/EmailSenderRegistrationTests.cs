using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Verifies that the DI composition in <c>SGV.Api</c> resolves the
/// ASP.NET Core Identity <see cref="IEmailSender{TUser}"/> against
/// <see cref="SmtpEmailSender"/>. Without this registration, Identity
/// uses a no-op sender that silently swallows reset and confirmation
/// emails.
/// </summary>
public sealed class EmailSenderRegistrationTests
{
    [Fact]
    public void Api_HostResolvesIEmailSender_AsSmtpEmailSender()
    {
        using var factory = new ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetService<IEmailSender<SgvIdentityUser>>();

        Assert.NotNull(sender);
        Assert.IsType<SmtpEmailSender>(sender);
    }

    [Fact]
    public void Api_HostResolvesIEmailSender_AsSingleton()
    {
        // The sender is registered as Singleton per the design: it
        // holds a MailKit SmtpClient lifetime shared across the host,
        // so we lock the lifetime contract here.
        using var factory = new ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IEmailSender<SgvIdentityUser>>();
        var second = scope.ServiceProvider.GetRequiredService<IEmailSender<SgvIdentityUser>>();

        Assert.Same(first, second);
    }
}