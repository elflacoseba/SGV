using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Skips the test when the MySQL server configured for tests is not reachable.
/// Use this for tests that actually connect to the database (migration
/// application, data operations). Model-level tests that only inspect EF
/// metadata do NOT need this attribute.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    private static readonly bool MySqlAvailable = CheckMySqlAvailability();

    public MySqlFactAttribute()
    {
        if (!MySqlAvailable)
        {
            Skip = "MySQL server is not available";
        }
    }

    private static bool CheckMySqlAvailability()
    {
        try
        {
            using var context = new TestSgvDbContextFactory().CreateDbContext([]);

            if (!context.Database.CanConnect())
            {
                return false;
            }

            // Bootstrap the test database schema once per test session. Migrate
            // is idempotent: it creates the database if it doesn't exist and
            // applies only the pending migrations. Tests that depend on a clean
            // schema (auditoria interceptor, repos, unique constraints) can
            // then insert/update/delete against sgv_test without extra setup.
            context.Database.Migrate();

            return true;
        }
        catch
        {
            // Any connection failure (network, auth, missing server) → skip
            return false;
        }
    }
}
