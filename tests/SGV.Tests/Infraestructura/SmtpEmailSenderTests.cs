using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Tests for <see cref="SmtpEmailSender"/> and its public link builder.
/// Two surfaces are covered: the URL-encoded link format mandated by
/// the password reset spec, and the Logger transport that drops the
/// message into <see cref="ILogger{TCategoryName}"/> instead of opening
/// a real SMTP connection.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    [Fact]
    public void BuildPasswordResetLink_EncodesUserIdAndToken()
    {
        const string webBaseUrl = "https://sgv.example.com";
        const string userId = "abc";
        const string token = "+a/b=";

        var link = SmtpEmailSender.BuildPasswordResetLink(webBaseUrl, userId, token);

        Assert.Equal(
            "https://sgv.example.com/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D",
            link);
    }

    [Fact]
    public void BuildPasswordResetLink_TrimsTrailingSlashOnBaseUrl()
    {
        const string webBaseUrl = "https://sgv.example.com/";
        const string userId = "u-1";
        const string token = "plain-token";

        var link = SmtpEmailSender.BuildPasswordResetLink(webBaseUrl, userId, token);

        Assert.Equal(
            "https://sgv.example.com/auth/reset-password?userId=u-1&token=plain-token",
            link);
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_LoggerMode_WritesSubjectAndBodyToLogger()
    {
        var options = Options.Create(new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            Host = "unused",
            Port = 25,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
        });
        var sink = new ListLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(options, sink);
        var user = new SgvIdentityUser { Email = "user@example.com" };

        await sender.SendPasswordResetLinkAsync(
            user,
            link: "https://sgv.example.com/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D",
            htmlMessage: "<p>Click the link</p>");

        Assert.Contains(sink.Records, r =>
            r.Level == LogLevel.Information
            && r.Message.Contains("user@example.com", StringComparison.Ordinal)
            && r.Message.Contains("Restablecé tu contraseña", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendPasswordResetAsync_LoggerMode_BuildsUrlEncodedLinkInBody()
    {
        var options = Options.Create(new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            Host = "unused",
            Port = 25,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = "https://sgv.example.com"
        });
        var sink = new ListLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(options, sink);

        await sender.SendPasswordResetAsync(
            userId: "abc",
            token: "+a/b=");

        var body = sink.Records.Select(r => r.Message).FirstOrDefault() ?? string.Empty;
        Assert.Contains(
            "https://sgv.example.com/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D",
            body,
            StringComparison.Ordinal);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Records.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}