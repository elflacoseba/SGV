using MySqlConnector;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Focused tests for <see cref="TestSgvDbContextFactory"/> helpers.
/// These tests verify the contract of the factory's static members
/// without creating a database connection.
/// </summary>
public sealed class TestSgvDbContextFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildConnectionStringForDatabase_NullEmptyOrWhitespace_ThrowsArgumentException(string? databaseName)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => TestSgvDbContextFactory.BuildConnectionStringForDatabase(databaseName!));

        Assert.Equal("databaseName", ex.ParamName);
    }

    [Fact]
    public void BuildConnectionStringForDatabase_ValidName_ReturnsConnectionStringWithThatDatabase()
    {
        const string dbName = "SGV_Test_Helper_UnitTest";

        var result = TestSgvDbContextFactory.BuildConnectionStringForDatabase(dbName);

        Assert.Contains(dbName, result, StringComparison.OrdinalIgnoreCase);

        // Verify it parses as a valid MySQL connection string with the expected database
        var builder = new MySqlConnectionStringBuilder(result);
        Assert.Equal(dbName, builder.Database);
    }
}
