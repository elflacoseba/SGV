using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;

namespace SGV.Tests.Persistencia;

internal enum MySqlTestDatabaseStatus
{
    Available,
    ServerUnavailable,
    BootstrapFailure,
}

internal sealed record TestMySqlConnectionSettings(
    string ConnectionString,
    string Source,
    string RedactedConnectionString);

internal sealed record MySqlTestDatabaseAvailability(
    MySqlTestDatabaseStatus Status,
    string Message,
    Exception? Exception)
{
    public bool IsAvailable => Status == MySqlTestDatabaseStatus.Available;

    public bool ShouldSkip(bool isCi) => Status == MySqlTestDatabaseStatus.ServerUnavailable && !isCi;
}

internal static partial class MySqlTestDatabaseBootstrap
{
    /// <summary>
    /// Cached once per test-session via <see cref="Lazy{T}"/>. This means
    /// the probe runs exactly once: the first [MySqlFact] triggers it and
    /// the result is frozen for the session. If MySQL becomes available
    /// mid-session (unlikely in CI, possible in local), subsequent tests
    /// still see the cached state. Acceptable trade-off for performance.
    /// </summary>
    private static readonly Lazy<MySqlTestDatabaseAvailability> CachedAvailability = new(CheckAvailability);

    /// <summary>MySqlException 1042 — Can't get hostname for your address.</summary>
    private const int ER_BAD_HOST_ERROR = 1042;
    /// <summary>MySqlException 1045 — Access denied (bad credentials).</summary>
    private const int ER_ACCESS_DENIED_ERROR = 1045;
    /// <summary>MySqlException 2002 — Can't connect to server (socket).</summary>
    private const int CR_CONNECTION_ERROR = 2002;
    /// <summary>MySqlException 2003 — Can't connect to MySQL server.</summary>
    private const int CR_CONN_HOST_ERROR = 2003;

    /// <summary>Maximum depth when walking <see cref="Exception.InnerException"/> chains.</summary>
    private const int MaxExceptionDepth = 10;

    public static MySqlTestDatabaseAvailability GetAvailability() => CachedAvailability.Value;

    internal static MySqlTestDatabaseAvailability Evaluate(
        TestMySqlConnectionSettings settings,
        Func<bool> canConnect,
        Action migrate,
        Action<string>? log)
    {
        try
        {
            if (!canConnect())
            {
                return ReportUnavailable(settings, "Database.CanConnect() returned false.", null, log);
            }
        }
        catch (Exception exception) when (IsServerUnavailable(exception))
        {
            return ReportUnavailable(settings, "Connectivity probe failed.", exception, log);
        }
        catch (Exception exception)
        {
            return ReportBootstrapFailure(settings, "Connectivity probe failed unexpectedly.", exception, log);
        }

        try
        {
            migrate();
            return new MySqlTestDatabaseAvailability(
                MySqlTestDatabaseStatus.Available,
                BuildMessage("MySQL test database is available.", settings, null),
                null);
        }
        catch (Exception exception)
        {
            return ReportBootstrapFailure(settings, "MySQL test database bootstrap failed during migration.", exception, log);
        }
    }

    private static MySqlTestDatabaseAvailability CheckAvailability()
    {
        var settings = TestSgvDbContextFactory.ResolveSettings();

        return Evaluate(
            settings,
            canConnect: () =>
            {
                using var context = TestSgvDbContextFactory.CreateDbContext(settings);
                return context.Database.CanConnect();
            },
            migrate: () =>
            {
                using var context = TestSgvDbContextFactory.CreateDbContext(settings);
                context.Database.Migrate();
            },
            log: message => Console.Error.WriteLine(message));
    }

    private static MySqlTestDatabaseAvailability ReportUnavailable(
        TestMySqlConnectionSettings settings,
        string reason,
        Exception? exception,
        Action<string>? log)
    {
        var message = BuildMessage("MySQL server is not available for persistence tests.", settings, reason, exception);
        log?.Invoke(message);
        return new MySqlTestDatabaseAvailability(MySqlTestDatabaseStatus.ServerUnavailable, message, exception);
    }

    private static MySqlTestDatabaseAvailability ReportBootstrapFailure(
        TestMySqlConnectionSettings settings,
        string reason,
        Exception exception,
        Action<string>? log)
    {
        var message = BuildMessage(reason, settings, exception: exception);
        log?.Invoke(message);
        return new MySqlTestDatabaseAvailability(MySqlTestDatabaseStatus.BootstrapFailure, message, exception);
    }

    private static string BuildMessage(
        string summary,
        TestMySqlConnectionSettings settings,
        string? reason = null,
        Exception? exception = null)
    {
        var details = reason is not null
            ? reason
            : exception is not null
                ? RedactMessage(exception.Message)
                : null;
        return details is null
            ? $"{summary} Source: {settings.Source}. Connection: {settings.RedactedConnectionString}"
            : $"{summary} Source: {settings.Source}. Connection: {settings.RedactedConnectionString}. Reason: {details}";
    }

    /// <summary>
    /// Redacts sensitive patterns (e.g. Password=...) from a diagnostic message.
    /// This is a best-effort safety net — it does NOT guarantee full sanitization.
    /// </summary>
    internal static string RedactMessage(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return PasswordPattern().Replace(text, "Password=<redacted>");
    }

    [GeneratedRegex(@"Password\s*=\s*[^;]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordPattern();

    /// <summary>
    /// Classifies an exception as "server unavailable" (safe to skip locally).
    /// Walks up to <see cref="MaxExceptionDepth"/> InnerException levels using
    /// an iterative loop to avoid unbounded recursion.
    /// </summary>
    private static bool IsServerUnavailable(Exception exception)
    {
        var current = exception;
        for (var depth = 0; current is not null && depth < MaxExceptionDepth; depth++)
        {
            switch (current)
            {
                // Network-level errors — server genuinely unreachable.
                case MySqlException { Number: 0 }:
                case MySqlException { Number: ER_BAD_HOST_ERROR }:
                case MySqlException { Number: CR_CONNECTION_ERROR }:
                case MySqlException { Number: CR_CONN_HOST_ERROR }:
                case SocketException:
                case TimeoutException:
                    return true;

                // ER_ACCESS_DENIED_ERROR (1045) intentionally NOT classified as
                // ServerUnavailable: the server is reachable, credentials are
                // wrong. This is a bootstrap/configuration failure and should
                // fail loud even locally so the developer fixes auth, not skips.
            }

            current = current.InnerException;
        }

        return false;
    }
}
