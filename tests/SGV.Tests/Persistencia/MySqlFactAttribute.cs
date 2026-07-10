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
    private static readonly MySqlTestDatabaseAvailability Availability = MySqlTestDatabaseBootstrap.GetAvailability();

    public MySqlFactAttribute()
        : this(Availability, TestSgvDbContextFactory.IsRunningInCi())
    {
    }

    internal MySqlFactAttribute(MySqlTestDatabaseAvailability availability, bool isCi)
    {
        ArgumentNullException.ThrowIfNull(availability);

        if (availability.IsAvailable)
        {
            return;
        }

        if (availability.ShouldSkip(isCi))
        {
            Skip = availability.Message;
            return;
        }

        throw new InvalidOperationException(availability.Message, availability.Exception);
    }
}
