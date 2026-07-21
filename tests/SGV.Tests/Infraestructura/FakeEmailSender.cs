using Microsoft.AspNetCore.Identity;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Captures outbound <see cref="Microsoft.AspNetCore.Identity.IEmailSender{TUser}"/>
/// payloads for assertion in tests. Implements the same
/// <see cref="Microsoft.AspNetCore.Identity.IEmailSender{TUser}"/>
/// surface that <see cref="SmtpEmailSender"/> satisfies.
/// </summary>
internal sealed class FakeEmailSender : Microsoft.AspNetCore.Identity.IEmailSender<SgvIdentityUser>
{
    public sealed record EmailCall(string To, string Subject, string HtmlBody);

    public List<EmailCall> Calls { get; } = new();

    public Task SendConfirmationLinkAsync(SgvIdentityUser user, string link, string htmlMessage)
    {
        Calls.Add(new EmailCall(link, htmlMessage, user.Email ?? string.Empty));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(SgvIdentityUser user, string link, string htmlMessage)
    {
        Calls.Add(new EmailCall(user.Email ?? string.Empty, htmlMessage, link));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(SgvIdentityUser user, string email, string resetCode)
    {
        Calls.Add(new EmailCall(email, resetCode, user.Email ?? string.Empty));
        return Task.CompletedTask;
    }
}
