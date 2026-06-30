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
            return context.Database.CanConnect();
        }
        catch
        {
            // Any connection failure (network, auth, missing server) → skip
            return false;
        }
    }
}
