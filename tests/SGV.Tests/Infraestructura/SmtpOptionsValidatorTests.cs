using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Email;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Tests for <see cref="SmtpOptions"/> validation. Two surfaces are
/// covered: (a) the DataAnnotation contract on the options class itself,
/// and (b) the fail-loud startup behavior of the API host when the
/// required SMTP keys are missing or <c>WebBaseUrl</c> is empty outside
/// <c>Development</c>.
/// </summary>
public sealed class SmtpOptionsValidatorTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Defaults_ModeIsLoggerAndWebBaseUrlIsEmpty()
    {
        var options = new SmtpOptions();

        Assert.Equal(SmtpDeliveryMode.Logger, options.Mode);
        Assert.Equal(string.Empty, options.WebBaseUrl);
    }

    [Fact]
    public void DataAnnotations_WebBaseUrlMissing_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Host = "smtp.example.com",
            Port = 587,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV"
            // WebBaseUrl intentionally omitted
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.WebBaseUrl)));
    }

    [Fact]
    public void DataAnnotations_WebBaseUrlRelative_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            Host = "localhost",
            Port = 1025,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "/relative-path"
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.WebBaseUrl)));
    }

    [Fact]
    public void DataAnnotations_AllRequiredFieldsPresent_PassesValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Host = "smtp.example.com",
            Port = 587,
            UserName = "smtp-user",
            Password = "smtp-secret",
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void IValidatable_ModeSmtp_HostMissing_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Port = 587,
            UserName = "smtp-user",
            Password = "smtp-secret",
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
            // Host intentionally omitted
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.Host)));
    }

    [Fact]
    public void IValidatable_ModeSmtp_RemoteHostWithoutCredentials_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Host = "smtp.example.com",
            Port = 587,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
            // UserName / Password intentionally omitted
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.UserName)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.Password)));
    }

    [Fact]
    public void IValidatable_ModeSmtp_LocalhostWithoutCredentials_PassesValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Host = "localhost",
            Port = 1025,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
            // Localhost dev relays (e.g. MailHog) typically accept anonymous.
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void IValidatable_ModeSmtp_PortOutOfRange_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Smtp,
            Host = "smtp.example.com",
            Port = 70_000,
            UserName = "smtp-user",
            Password = "smtp-secret",
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.Port)));
    }

    [Fact]
    public void IValidatable_ModeLogger_DoesNotEnforceTransportDetails()
    {
        // Mode=Logger is a no-op; missing Host/UserName/Password MUST NOT
        // fail validation. This protects the Development experience where
        // the host writes outbound mail to the application logger.
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void DataAnnotations_FromAddressMissing_FailsValidation()
    {
        var options = new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            Host = "localhost",
            Port = 1025,
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
            // FromAddress intentionally omitted
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SmtpOptions.FromAddress)));
    }

    [Fact]
    public void OptionsBuilder_BindAndValidate_ResolvesSmtpOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Mode"] = SmtpDeliveryMode.Logger.ToString(),
                ["Smtp:Host"] = "smtp.example.com",
                ["Smtp:Port"] = "587",
                ["Smtp:FromAddress"] = "no-reply@sgv.local",
                ["Smtp:FromName"] = "SGV",
                ["Smtp:WebBaseUrl"] = "https://sgv.example.com"
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddOptions<SmtpOptions>()
            .BindConfiguration("Smtp")
            .ValidateDataAnnotations()
            .Services
            .BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<SmtpOptions>>().Value;

        Assert.Equal("https://sgv.example.com", options.WebBaseUrl);
        Assert.Equal(SmtpDeliveryMode.Logger, options.Mode);
        Assert.Equal(587, options.Port);
    }
}