using Xunit;
using Microsoft.Extensions.Configuration;

namespace SGV.Tests.Persistencia;

public sealed class MySqlTestDatabaseBootstrapTests
{
    private static readonly TestMySqlConnectionSettings Settings = new(
        TestSgvDbContextFactory.LocalDevConnectionString,
        "LocalDevConnectionString",
        "Server=localhost;Port=3306;Database=sgv_test;User=root;Password=<redacted>;");

    [Fact]
    public void Evaluate_WhenCanConnectReturnsFalse_ReturnsUnavailableResult()
    {
        var messages = new List<string>();

        var result = MySqlTestDatabaseBootstrap.Evaluate(
            Settings,
            canConnect: () => false,
            migrate: () => throw new InvalidOperationException("Should not migrate when unavailable"),
            log: messages.Add);

        Assert.Equal(MySqlTestDatabaseStatus.ServerUnavailable, result.Status);
        Assert.Contains("CanConnect() returned false", result.Message, StringComparison.Ordinal);
        Assert.Equal(result.Message, Assert.Single(messages));
        Assert.Contains("Source: LocalDevConnectionString", result.Message, StringComparison.Ordinal);
        Assert.Contains("Password=<redacted>", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_WhenConnectivityThrowsServerException_ClassifiesAsUnavailable()
    {
        var result = MySqlTestDatabaseBootstrap.Evaluate(
            Settings,
            canConnect: () => throw new TimeoutException("Connection timed out"),
            migrate: static () => { },
            log: null);

        Assert.Equal(MySqlTestDatabaseStatus.ServerUnavailable, result.Status);
        Assert.IsType<TimeoutException>(result.Exception);
    }

    [Fact]
    public void Evaluate_WhenMigrationThrows_ReturnsBootstrapFailure()
    {
        var messages = new List<string>();

        var result = MySqlTestDatabaseBootstrap.Evaluate(
            Settings,
            canConnect: () => true,
            migrate: () => throw new InvalidOperationException("Broken migration"),
            log: messages.Add);

        Assert.Equal(MySqlTestDatabaseStatus.BootstrapFailure, result.Status);
        Assert.Contains("migration", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal(result.Message, Assert.Single(messages));
        Assert.Contains("Source: LocalDevConnectionString", result.Message, StringComparison.Ordinal);
        Assert.Contains("Password=<redacted>", result.Message, StringComparison.Ordinal);
        Assert.Contains("Broken migration", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSettings_WhenEnvironmentVariableIsUsed_RedactsPasswordInDiagnostics()
    {
        const string connectionString = "Server=127.0.0.1;Port=3306;Database=sgv_test;User=root;Password=super-secret;";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SgvDatabase"] = connectionString,
            })
            .Build();

        var settings = TestSgvDbContextFactory.ResolveSettings(configuration, connectionString);

        Assert.Equal("ConnectionStrings__SgvDatabase", settings.Source);
        Assert.Contains("Password=<redacted>", settings.RedactedConnectionString, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", settings.RedactedConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void MySqlFactAttribute_WhenLocalAndUnavailable_SkipsWithUsefulRedactedMessage()
    {
        var availability = new MySqlTestDatabaseAvailability(
            MySqlTestDatabaseStatus.ServerUnavailable,
            "MySQL server is not available for persistence tests. Source: ConnectionStrings__SgvDatabase. Connection: Server=127.0.0.1;Port=1;Database=sgv_stub;User=stub;Password=<redacted>;. Reason: Connectivity probe failed.",
            new TimeoutException("Connection timed out"));

        var attribute = new MySqlFactAttribute(availability, isCi: false);

        Assert.Equal(availability.Message, attribute.Skip);
        Assert.Contains("Source: ConnectionStrings__SgvDatabase", attribute.Skip, StringComparison.Ordinal);
        Assert.Contains("Password=<redacted>", attribute.Skip, StringComparison.Ordinal);
        Assert.DoesNotContain("Connection timed out", attribute.Skip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MySqlFactAttribute_WhenCiAndUnavailable_FailsLoud()
    {
        var availability = new MySqlTestDatabaseAvailability(
            MySqlTestDatabaseStatus.ServerUnavailable,
            "MySQL server is not available for persistence tests. Source: ConnectionStrings__SgvDatabase. Connection: Server=127.0.0.1;Port=1;Database=sgv_stub;User=stub;Password=<redacted>;. Reason: Connectivity probe failed.",
            new TimeoutException("Connection timed out"));

        var exception = Assert.Throws<InvalidOperationException>(() => new MySqlFactAttribute(availability, isCi: true));

        Assert.Equal(availability.Message, exception.Message);
        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    [Fact]
    public void MySqlFactAttribute_WhenBootstrapFails_FailsLoud()
    {
        var availability = new MySqlTestDatabaseAvailability(
            MySqlTestDatabaseStatus.BootstrapFailure,
            "MySQL test database bootstrap failed during migration. Source: LocalDevConnectionString. Connection: Server=localhost;Port=3306;Database=sgv_test;User=root;Password=<redacted>;. Reason: Broken migration",
            new InvalidOperationException("Broken migration"));

        var exception = Assert.Throws<InvalidOperationException>(() => new MySqlFactAttribute(availability, isCi: false));

        Assert.Equal(availability.Message, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void MySqlFactAttribute_WhenCiAndBootstrapFails_FailsLoudWithInnerException()
    {
        var availability = new MySqlTestDatabaseAvailability(
            MySqlTestDatabaseStatus.BootstrapFailure,
            "MySQL test database bootstrap failed during migration. Source: ConnectionStrings__SgvDatabase. Connection: Server=127.0.0.1;Port=3306;Database=sgv_test;User=root;Password=<redacted>;. Reason: Broken migration",
            new InvalidOperationException("Broken migration"));

        var exception = Assert.Throws<InvalidOperationException>(() => new MySqlFactAttribute(availability, isCi: true));

        Assert.Equal(availability.Message, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void RedactMessage_WhenTextContainsPassword_ReplacesIt()
    {
        const string input = "Server=localhost;Password=super-secret;Database=sgv_test";
        var result = MySqlTestDatabaseBootstrap.RedactMessage(input);
        Assert.Contains("Password=<redacted>", result, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactMessage_WhenTextDoesNotContainPassword_ReturnsUnchanged()
    {
        const string input = "Connection timed out after 30 seconds";
        var result = MySqlTestDatabaseBootstrap.RedactMessage(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void RedactMessage_WhenTextIsNull_ReturnsEmpty()
    {
        var result = MySqlTestDatabaseBootstrap.RedactMessage(null);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShouldSkip_ReturnsFalseInCi(bool isCi, bool expected)
    {
        var availability = new MySqlTestDatabaseAvailability(
            MySqlTestDatabaseStatus.ServerUnavailable,
            "server unavailable",
            null);

        Assert.Equal(expected, availability.ShouldSkip(isCi));
    }
}
