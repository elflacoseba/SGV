namespace SGV.Infraestructura.Email;

/// <summary>
/// Selects the transport used by <see cref="SmtpEmailSender"/> when
/// delivering outbound email. The defaults are tuned for local
/// development — <see cref="Logger"/> writes the message to the
/// application logger without making a network call, while
/// <see cref="Smtp"/> uses MailKit to connect to a real SMTP server.
/// </summary>
public enum SmtpDeliveryMode
{
    Logger,
    Smtp
}