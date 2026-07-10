using System.Text.RegularExpressions;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Regression guard: detects hardcoded MySQL connection strings inside the
/// persistence test suite. Persistence tests MUST resolve their connection
/// string through <see cref="TestSgvDbContextFactory"/> (env var
/// <c>ConnectionStrings__SgvDatabase</c>, an appsettings file, or the
/// <c>LocalDevConnectionString</c> default) and only override the
/// <c>Database</c> segment per-test via
/// <see cref="TestSgvDbContextFactory.BuildConnectionStringForDatabase"/>.
///
/// Hardcoded server/user literals break CI (which uses a password) and any
/// developer whose local MySQL deviates from the <c>root</c>/no-password
/// default. See issue #99.
/// </summary>
public sealed class NoHardcodedMySqlConnectionStringsTests
{
    private const string HardcodedServerUserPattern =
        @"(?i)Server\s*=\s*[^;""']*localhost[^;""']*User\s*=\s*root";

    private const string HardcodedEmptyPasswordPattern =
        @"(?i)Password\s*=\s*;";

    /// <summary>
    /// Files allowed to mention hardcoded MySQL connection-string fragments.
    /// The factory is the single source of truth for those literals, and the
    /// bootstrap tests assert on the redacted representation as part of their
    /// contract.
    /// </summary>
    private static readonly HashSet<string> WhitelistedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "TestSgvDbContextFactory.cs",
        "MySqlTestDatabaseBootstrapTests.cs",
        "NoHardcodedMySqlConnectionStringsTests.cs",
    };

    [Fact]
    public void PersistenciaTests_NoContienenConnectionStringsMySqlHardcodeadas()
    {
        var directory = ResolvePersistenciaDirectory();
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();

        var violations = new List<string>();
        var combinedRegex = new Regex(
            $"({HardcodedServerUserPattern})|({HardcodedEmptyPasswordPattern})",
            RegexOptions.CultureInvariant);

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (WhitelistedFiles.Contains(name))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!combinedRegex.IsMatch(line))
                {
                    continue;
                }

                violations.Add(
                    $"{name}:{i + 1}: {line.Trim()}");
            }
        }

        Assert.Empty(violations);
    }

    private static string ResolvePersistenciaDirectory()
    {
        // AppContext.BaseDirectory when running `dotnet test` from the repo root is
        // `tests/SGV.Tests/bin/Debug/net10.0/`. Five `..` get us back to the workspace.
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "SGV.Tests", "Persistencia"));

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"Could not resolve Persistencia test directory from '{AppContext.BaseDirectory}'. " +
                $"Expected '{path}'. Run tests from the repository root via `dotnet test`.");
        }

        return path;
    }
}