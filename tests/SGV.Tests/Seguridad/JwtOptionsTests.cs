using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Fail-loud tests for <see cref="SGV.Contracts.Seguridad.JwtOptions.SigningKey"/>.
///
/// The validator wired in <c>SGV.Api/Program.cs</c> must:
/// <list type="bullet">
/// <item>reject an absent or blank <c>Jwt:SigningKey</c> at host build time;</item>
/// <item>reject a key whose UTF-8 byte length is &lt; 32;</item>
/// <item>accept the pinned dev placeholder that ships in <c>appsettings.Development.json</c>.</item>
/// </list>
///
/// Tests rely on <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// to trigger <c>ValidateOnStart</c>: <c>CreateClient()</c> builds the host and surfaces any
/// <see cref="OptionsValidationException"/> to the caller.
/// </summary>
public sealed class JwtOptionsTests
{
    private const string SigningKeyConfigKey = "Jwt:SigningKey";

    [Fact]
    public void HostBuild_SinSigningKey_LanzaOptionsValidationException()
    {
        using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?> { [SigningKeyConfigKey] = string.Empty })));

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(SigningKeyConfigKey, ex.Message);
    }

    [Fact]
    public void HostBuild_SigningKeyCorto_LanzaOptionsValidationException()
    {
        using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?> { [SigningKeyConfigKey] = "short-key" })));

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("32 UTF-8 bytes", ex.Message);
    }

    [Fact]
    public void HostBuild_SigningKey31Bytes_Lanza()
    {
        // 31 ASCII chars — UTF-8 byte count == 31, must fail the >= 32 validator.
        var clave31Bytes = new string('a', 31);

        using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?> { [SigningKeyConfigKey] = clave31Bytes })));

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("32 UTF-8 bytes", ex.Message);
    }

    [Fact]
    public void HostBuild_SigningKey32Bytes_Arranca()
    {
        // 32 ASCII chars — UTF-8 byte count == 32, must pass the >= 32 validator.
        var clave32Bytes = new string('b', 32);

        using var factory = new WebApplicationFactory<SGV.Api.Program>()
            .WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?> { [SigningKeyConfigKey] = clave32Bytes })));

        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    /// <summary>
    /// Regression guard: the dev placeholder shipped in <c>appsettings.Development.json</c> must
    /// pass validation so a fresh <c>dotnet run</c> works without additional setup.
    /// </summary>
    [Fact]
    public void HostBuild_PlaceholderDev_Arranca()
    {
        using var factory = new WebApplicationFactory<SGV.Api.Program>();
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    /// <summary>
    /// Structural guard: the repository must keep the placeholder recognisable (>=32 bytes UTF-8).
    /// Cheap to run, fails loud if someone deletes or trims it.
    /// </summary>
    [Fact]
    public void appsettings_Development_Tiene_Placeholder_Valido_MayorIgual32Bytes()
    {
        var repoRoot = LocateRepoRoot();
        var path = Path.Combine(repoRoot, "src", "SGV.Api", "appsettings.Development.json");
        Assert.True(File.Exists(path), $"Expected dev settings at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var signingKey = doc.RootElement
            .GetProperty("Jwt")
            .GetProperty("SigningKey")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(signingKey));
        Assert.True(
            Encoding.UTF8.GetByteCount(signingKey!) >= 32,
            "Placeholder Jwt:SigningKey must be >=32 UTF-8 bytes to satisfy the validator");
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
