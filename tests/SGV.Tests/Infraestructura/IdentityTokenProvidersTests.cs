using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Verifies that the Identity composition in <c>SGV.Api/Program.cs</c>
/// registers the default token providers AND configures the reset token
/// lifespan to one hour. Without <c>AddDefaultTokenProviders</c>,
/// <see cref="UserManager{TUser}.GeneratePasswordResetTokenAsync"/> throws
/// <see cref="InvalidOperationException"/> because no provider is mapped
/// to <c>TokenOptions.DefaultProvider</c>. The lifespan lives on
/// <see cref="DataProtectionTokenProviderOptions"/>.
/// </summary>
public sealed class IdentityTokenProvidersTests
{
    [Fact]
    public void Api_HostIdentityComposition_RegistersDefaultTokenProviders()
    {
        var tokens = ResolveIdentityOptions().Tokens;

        Assert.NotEmpty(tokens.ProviderMap);
        Assert.True(
            tokens.ProviderMap.ContainsKey(TokenOptions.DefaultProvider),
            $"Expected provider map to contain '{TokenOptions.DefaultProvider}'.");
        Assert.False(
            string.IsNullOrWhiteSpace(tokens.PasswordResetTokenProvider),
            "Expected PasswordResetTokenProvider name to be populated.");
    }

    [Fact]
    public void Api_HostIdentityComposition_ConfiguresOneHourPasswordResetLifespan()
    {
        var options = ResolveDataProtectionTokenProviderOptions();

        Assert.Equal(TimeSpan.FromHours(1), options.TokenLifespan);
    }

    private static IdentityOptions ResolveIdentityOptions()
    {
        using var factory = new ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<IdentityOptions>>()
            .CurrentValue;
    }

    private static DataProtectionTokenProviderOptions ResolveDataProtectionTokenProviderOptions()
    {
        using var factory = new ApiIntegrationFixture().RootFactory;
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<DataProtectionTokenProviderOptions>>()
            .CurrentValue;
    }
}