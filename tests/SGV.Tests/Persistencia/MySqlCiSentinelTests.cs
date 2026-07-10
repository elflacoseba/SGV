using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class MySqlCiSentinelTests
{
    [Fact]
    public void MySqlProbe_IsAvailable_WhenRunningInCi()
    {
        if (!TestSgvDbContextFactory.IsRunningInCi())
        {
            return;
        }

        var availability = MySqlTestDatabaseBootstrap.GetAvailability();

        Assert.True(availability.IsAvailable, availability.Message);
    }
}
