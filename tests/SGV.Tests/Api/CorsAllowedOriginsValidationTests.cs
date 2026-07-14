using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Fail-loud tests for the <c>AllowedOrigins</c> validation in <c>SGV.Api/Program.cs</c>.
///
/// The composition root must:
/// <list type="bullet">
/// <item>throw <see cref="InvalidOperationException"/> when the host is built outside
/// <c>Development</c> and the <c>AllowedOrigins</c> section is absent or empty;</item>
/// <item>start successfully when <c>AllowedOrigins</c> has at least one entry, regardless of
/// environment;</item>
/// <item>start successfully in <c>Development</c> even when <c>AllowedOrigins</c> is empty,
/// falling back to an explicit dev-only policy that does not combine
/// <c>AllowAnyOrigin()</c> with <c>AllowCredentials()</c>.</item>
/// </list>
///
/// These tests run against the real composition root via
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>, so the
/// configured <c>Jwt:SigningKey</c> must be ≥32 UTF-8 bytes to satisfy
/// <c>ValidateOnStart</c>.
/// </summary>
public sealed class CorsAllowedOriginsValidationTests
{
    private const string JwtSigningKeyConfigKey = "Jwt:SigningKey";
    private const string AllowedOriginsConfigKey = "AllowedOrigins";
    private const string ConnectionStringConfigKey = "ConnectionStrings:SgvDatabase";
    private const string DevValidSigningKey = "0123456789abcdef0123456789abcdef";
    private const string DevValidConnectionString = "Server=localhost;Database=sgv_test;Uid=root;Connection Timeout=5;";

    [Fact]
    public async Task HostBuild_Production_SinAllowedOrigins_LanzaInvalidOperationException()
    {
        // Arrange — Production env with a valid JWT key and no AllowedOrigins override.
        // The override collection intentionally omits "AllowedOrigins" so the section is
        // absent (not just empty) from the effective configuration.
        await using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Production")
                .UseSetting(JwtSigningKeyConfigKey, DevValidSigningKey)
                .UseSetting(ConnectionStringConfigKey, DevValidConnectionString));

        // Act + Assert — CreateClient triggers host build; the validator must throw before
        // the host is fully constructed.
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains(AllowedOriginsConfigKey, ex.Message);
    }

    [Fact]
    public async Task HostBuild_Production_AllowedOriginsPoblado_Arranca()
    {
        // Arrange — Production env with a valid JWT key and an explicit AllowedOrigins entry.
        await using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Production")
                .UseSetting(JwtSigningKeyConfigKey, DevValidSigningKey)
                .UseSetting(ConnectionStringConfigKey, DevValidConnectionString)
                .UseSetting("AllowedOrigins:0", "https://app.example.com"));

        // Act — host build must succeed; CreateClient must not throw.
        using var client = factory.CreateClient();

        // Assert — sanity check that the client is usable.
        Assert.NotNull(client);
    }

    [Fact]
    public async Task HostBuild_Development_AllowedOriginsVacio_Arranca()
    {
        // Arrange — Development env (the validator is bypassed). AllowedOrigins stays
        // absent from the override collection; the dev appsettings supplies the
        // placeholder, but for this test we still need a valid JWT key.
        await using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting(JwtSigningKeyConfigKey, DevValidSigningKey)
                .UseSetting(ConnectionStringConfigKey, DevValidConnectionString));

        // Act + Assert — host build succeeds; no exception is thrown.
        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    /// <summary>
    /// Structural regression guard: the production code must never combine
    /// <c>AllowAnyOrigin()</c> with <c>AllowCredentials()</c> in the same CORS policy.
    /// Browsers reject the combination and ASPIl's behaviour around it is not safe to
    /// rely on across versions.
    /// </summary>
    [Fact]
    public void ProgramCs_Api_NoContieneAllowAnyOrigin()
    {
        // Arrange — locate the SGV.Api Program.cs from the test runner's bin directory.
        var repoRoot = LocateRepoRoot();
        var programCsPath = Path.Combine(repoRoot, "src", "SGV.Api", "Program.cs");
        Assert.True(File.Exists(programCsPath), $"Expected Program.cs at {programCsPath}");

        // Act — read the source verbatim.
        var source = File.ReadAllText(programCsPath);

        // Assert — the implementation never references AllowAnyOrigin, regardless of
        // whether credentials are also requested. That makes the prohibited combination
        // structurally impossible.
        Assert.DoesNotContain("AllowAnyOrigin", source);
    }

    private static string LocateRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGV.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}