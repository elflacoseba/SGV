using System.ComponentModel.DataAnnotations;

namespace SGV.Infraestructura.Email;

/// <summary>
/// Strongly-typed configuration for outbound SMTP delivery. Bound from
/// the <c>Smtp</c> configuration section and validated at startup so
/// the API fails loud when the host URL or sender address are missing.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WebBaseUrl"/> MUST be an absolute URL (e.g.
/// <c>https://sgv.example.com</c>): the password reset email embeds
/// a recovery link built from this value, and a relative path would
/// produce a broken link inside the recipient's mailbox.
/// </para>
/// <para>
/// In <c>Development</c> the host tolerates a missing section because
/// <see cref="Mode"/> defaults to <see cref="SmtpDeliveryMode.Logger"/>
/// and the developer never actually sends mail. Any non-Development
/// environment MUST supply every required field; the API composition
/// root calls <c>ValidateDataAnnotations().ValidateOnStart()</c> on
/// this type. The cross-field contract (UserName/Password required
/// for non-localhost real SMTP) is enforced by
/// <see cref="IValidatableObject.Validate"/>, which
/// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
/// invokes when called with <c>validateAllProperties: true</c> — the
/// same mode <c>ValidateDataAnnotations()</c> uses internally.
/// </para>
/// </remarks>
public sealed class SmtpOptions : IValidatableObject
{
    /// <summary>Configuration section name. Matches the appsettings key.</summary>
    public const string SectionName = "Smtp";

    /// <summary>SMTP server host (e.g. <c>smtp.example.com</c>).</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server TCP port. Common values: 25, 465, 587.</summary>
    public int Port { get; set; } = 25;

    /// <summary>Whether to negotiate TLS/SSL on connect (MailKit SecureSocketOptions).</summary>
    public bool EnableSsl { get; set; }

    /// <summary>Optional username for SMTP AUTH. Empty means anonymous relay.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Optional password for SMTP AUTH.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>From address shown to recipients (RFC 5322).</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Smtp:FromAddress es obligatorio.")]
    [EmailAddress(ErrorMessage = "Smtp:FromAddress debe ser una dirección de email válida.")]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Friendly from name shown to recipients.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Smtp:FromName es obligatorio.")]
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute base URL of the web app. Used to construct password
    /// reset links inside outbound email bodies.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Smtp:WebBaseUrl es obligatorio.")]
    [Url(ErrorMessage = "Smtp:WebBaseUrl debe ser una URL absoluta (https://...).")]
    public string WebBaseUrl { get; set; } = string.Empty;

    /// <summary>Transport selection. Defaults to <see cref="SmtpDeliveryMode.Logger"/>.</summary>
    public SmtpDeliveryMode Mode { get; set; } = SmtpDeliveryMode.Logger;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Only enforce transport details when the host actually intends to
        // deliver mail. Mode=Logger is a no-op for development and tests.
        if (Mode != SmtpDeliveryMode.Smtp)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            yield return new ValidationResult(
                "Smtp:Host es obligatorio cuando Mode=Smtp.",
                new[] { nameof(Host) });
        }

        if (Port < 1 || Port > 65535)
        {
            yield return new ValidationResult(
                $"Smtp:Port debe estar entre 1 y 65535 (valor recibido: {Port}).",
                new[] { nameof(Port) });
        }

        // Real-world SMTP servers (anything not localhost) virtually always
        // require AUTH. Anonymous relay is only a dev convenience and would
        // fail silently at first send otherwise.
        if (!string.IsNullOrWhiteSpace(Host) && !IsLocalHost(Host))
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                yield return new ValidationResult(
                    "Smtp:UserName es obligatorio cuando el host no es localhost.",
                    new[] { nameof(UserName) });
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                yield return new ValidationResult(
                    "Smtp:Password es obligatorio cuando el host no es localhost.",
                    new[] { nameof(Password) });
            }
        }
    }

    private static bool IsLocalHost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || host.Equals("127.0.0.1", StringComparison.Ordinal)
           || host == "::1";
}
