using System.Reflection;
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
///
/// <para><b>Scope:</b> This guard detects the specific pattern that caused
/// issue #99: <c>Server=localhost;...;User=root;</c> with an empty password
/// (<c>Password=;</c>). It does <b>not</b> detect arbitrary hardcoded
/// credentials with other users or non-empty passwords — those are outside
/// the reported bug surface.</para>
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

    private static readonly Lazy<string> LazyPersistenciaDirectory = new(ResolvePersistenciaDirectory);

    [Fact]
    public void PersistenceTests_DoNotContainHardcodedMySqlConnectionStrings()
    {
        var directory = LazyPersistenciaDirectory.Value;
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
        // Search upward from the test assembly directory until we find the
        // repo root (identified by SGV.slnx). This is more robust than a
        // fixed number of parent hops because it tolerates changes to TFM,
        // build configuration, or project structure.
        const int maxDepth = 10;
        var candidate = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;

        for (var i = 0; i < maxDepth; i++)
        {
            if (File.Exists(Path.Combine(candidate, "SGV.slnx")))
            {
                var path = Path.GetFullPath(Path.Combine(
                    candidate, "tests", "SGV.Tests", "Persistencia"));

                if (Directory.Exists(path))
                {
                    return path;
                }

                throw new DirectoryNotFoundException(
                    $"Found repo root at '{candidate}' but the Persistencia test " +
                    $"directory does not exist at '{path}'.");
            }

            var parent = Directory.GetParent(candidate);
            if (parent is null)
            {
                break;
            }

            candidate = parent.FullName;
        }

        // Fallback: try the 5-level parent approach for compatibility
        var fallback = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "SGV.Tests", "Persistencia"));

        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve the Persistencia test directory. Tried " +
            $"searching upward from '{AppContext.BaseDirectory}' for " +
            $"SGV.slnx. Run tests via `dotnet test` from the repository root.");
    }
}
