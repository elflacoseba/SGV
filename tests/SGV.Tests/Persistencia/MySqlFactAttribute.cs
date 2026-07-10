using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Skips the test when the MySQL server configured for tests is not reachable.
/// Use this for tests that actually connect to the database (migration
/// application, data operations). Model-level tests that only inspect EF
/// metadata do NOT need this attribute.
/// 
/// The bootstrap probe runs exactly once per test-session via
/// <see cref="MySqlTestDatabaseBootstrap.GetAvailability"/> (backed by
/// <see cref="Lazy{T}"/>). If MySQL becomes available mid-session (local dev),
/// subsequent <c>[MySqlFact]</c> tests still see the cached state — an
/// acceptable performance trade-off since session restart is trivial.
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
