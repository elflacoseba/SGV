using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Skips the parameterized test when the MySQL server configured for tests
/// is not reachable. Mirrors <see cref="MySqlFactAttribute"/> but for
/// <see cref="TheoryAttribute"/>, so a parameterized matrix test can run
/// against a real database when available and SKIP cleanly otherwise.
///
/// The bootstrap probe runs exactly once per test-session via
/// <see cref="MySqlTestDatabaseBootstrap.GetAvailability"/> (backed by
/// <see cref="Lazy{T}"/>). The result is cached and shared with
/// <see cref="MySqlFactAttribute"/>; both attributes observe the same
/// availability state during a single test session.
/// </summary>
public sealed class MySqlTheoryAttribute : TheoryAttribute
{
    private static readonly MySqlTestDatabaseAvailability Availability = MySqlTestDatabaseBootstrap.GetAvailability();

    public MySqlTheoryAttribute()
        : this(Availability, TestSgvDbContextFactory.IsRunningInCi())
    {
    }

    internal MySqlTheoryAttribute(MySqlTestDatabaseAvailability availability, bool isCi)
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