using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Email;

/// <summary>
/// ASP.NET Core Identity <see cref="IEmailSender{TUser}"/> backed by
/// MailKit. Two transports are supported, selected by
/// <see cref="SmtpOptions.Mode"/>:
/// <list type="bullet">
/// <item><see cref="SmtpDeliveryMode.Logger"/> writes the rendered message
/// to <see cref="ILogger{TCategoryName}"/> without making a network call.
/// Useful for local development without an SMTP relay.</item>
/// <item><see cref="SmtpDeliveryMode.Smtp"/> connects to a real SMTP
/// server using MailKit and delivers the message.</item>
/// </list>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender<SgvIdentityUser>
{
    private readonly IOptionsMonitor<SmtpOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Builds the password reset link embedded in the recovery email.
    /// Public and static so callers (and tests) can compose the link
    /// without booting the SMTP stack.
    /// </summary>
    /// <remarks>
    /// The token is URL-encoded with <see cref="Uri.EscapeDataString"/>
    /// so reserved characters like <c>+</c>, <c>/</c> and <c>=</c>
    /// survive the trip through the user's mailbox and the
    /// <c>ResetPassword</c> query string without corruption.
    /// </remarks>
    public static string BuildPasswordResetLink(
        string webBaseUrl,
        string userId,
        string token)
    {
        ArgumentNullException.ThrowIfNull(webBaseUrl);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(token);

        var normalizedBase = webBaseUrl.TrimEnd('/');
        var encodedUserId = Uri.EscapeDataString(userId);
        var encodedToken = Uri.EscapeDataString(token);

        return $"{normalizedBase}/auth/reset-password?userId={encodedUserId}&token={encodedToken}";
    }

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = new StaticOptionsMonitor<SmtpOptions>(options.Value);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendConfirmationLinkAsync(SgvIdentityUser user, string link, string htmlMessage)
        => SendAsync(user?.Email ?? string.Empty, "Confirmá tu cuenta en SGV", htmlMessage);

    public Task SendPasswordResetLinkAsync(SgvIdentityUser user, string link, string htmlMessage)
        => SendAsync(user?.Email ?? string.Empty, "Restablecé tu contraseña en SGV", htmlMessage);

    public Task SendPasswordResetCodeAsync(SgvIdentityUser user, string email, string resetCode)
        => SendAsync(email, "Tu código de restablecimiento en SGV", resetCode);

    /// <summary>
    /// Composes a password reset email and dispatches it. The link is
    /// built via <see cref="BuildPasswordResetLink"/> so the token
    /// always travels URL-encoded.
    /// </summary>
    public Task SendPasswordResetAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var options = _options.CurrentValue;
        var link = BuildPasswordResetLink(options.WebBaseUrl, userId, token);

        const string subject = "Restablecé tu contraseña en SGV";
        var body =
            "<p>Recibimos un pedido para restablecer tu contraseña.</p>" +
            $"<p>Si fuiste vos, hacé clic en el siguiente enlace:</p>" +
            $"<p><a href=\"{link}\">Restablecer contraseña</a></p>" +
            "<p>Si no realizaste esta solicitud, podés ignorar este mensaje.</p>" +
            "<p>El enlace caduca en una hora.</p>";

        _logger.LogInformation(
            "SMTP password reset email composed for userId={UserId}; link={Link}",
            userId,
            link);

        return SendAsync(
            email: ResolveRecipientEmail(userId),
            subject: subject,
            htmlMessage: body);
    }

    private Task SendAsync(string email, string subject, string htmlMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(htmlMessage);

        var options = _options.CurrentValue;
        if (options.Mode == SmtpDeliveryMode.Logger)
        {
            LogToLogger(email, subject, htmlMessage, options);
            return Task.CompletedTask;
        }

        return SendViaMailKitAsync(email, subject, htmlMessage, options);
    }

    private static string ResolveRecipientEmail(string userId) => userId;

    private void LogToLogger(string email, string subject, string htmlMessage, SmtpOptions options)
    {
        _logger.LogInformation(
            "SMTP (Logger mode) -> from={From} to={To} subject={Subject} body={Body}",
            $"{options.FromName} <{options.FromAddress}>",
            email,
            subject,
            htmlMessage);
    }

    private async Task SendViaMailKitAsync(
        string email,
        string subject,
        string htmlMessage,
        SmtpOptions options)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlMessage };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        var socketOption = options.EnableSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(options.Host, options.Port, socketOption).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(options.UserName))
        {
            await client.AuthenticateAsync(options.UserName, options.Password).ConfigureAwait(false);
        }

        try
        {
            await client.SendAsync(message).ConfigureAwait(false);
        }
        finally
        {
            await client.DisconnectAsync(quit: true).ConfigureAwait(false);
        }
    }
}