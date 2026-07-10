using System.Net.Sockets;
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

internal static class MySqlTestDatabaseBootstrap
{
    private static readonly Lazy<MySqlTestDatabaseAvailability> CachedAvailability = new(CheckAvailability);

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
        var details = reason ?? exception?.Message;
        return details is null
            ? $"{summary} Source: {settings.Source}. Connection: {settings.RedactedConnectionString}"
            : $"{summary} Source: {settings.Source}. Connection: {settings.RedactedConnectionString}. Reason: {details}";
    }

    private static bool IsServerUnavailable(Exception exception)
    {
        return exception switch
        {
            MySqlException mySqlException when mySqlException.Number is 0 or 1042 or 1045 or 2002 or 2003 => true,
            SocketException => true,
            TimeoutException => true,
            _ when exception.InnerException is not null => IsServerUnavailable(exception.InnerException),
            _ => false,
        };
    }
}
