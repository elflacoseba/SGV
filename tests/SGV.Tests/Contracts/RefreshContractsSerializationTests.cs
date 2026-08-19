using System.Text.Json;
using System.Text.Json.Serialization;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Wire-type lock-down for the refresh-tokens contract surface that
/// PR1a (change <c>implementa-refresh-tokens</c>) introduces. Covers
/// REQ-AUTH-WIRE-1 (spec block A):
/// <list type="bullet">
/// <item><see cref="LoginResponse"/> MUST accept nullable
/// <c>RefreshToken</c> and <c>RefreshTokenExpiresAt</c> parameters
/// (with defaults) so every existing caller compiles unchanged —
/// nullable with default is the chosen back-compat mechanism (design
/// §2.9).</item>
/// <item><see cref="RefreshResponse"/> MUST round-trip through
/// <see cref="JsonSerializer"/> preserving all four properties.</item>
/// <item>The new <see cref="AuthApiRoutes"/> constants for refresh,
/// logout and the rate-limit policy name MUST exist and resolve to
/// stable paths.</item>
/// </list>
/// </summary>
public sealed class RefreshContractsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void RefreshResponse_RoundTrip_PreservaLasCuatroPropiedades()
    {
        var original = new RefreshResponse(
            AccessToken: "access-token-xyz",
            ExpiresAt: new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero),
            RefreshToken: "refresh-token-abc",
            RefreshTokenExpiresAt: new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RefreshResponse>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.AccessToken, deserialized!.AccessToken);
        Assert.Equal(original.ExpiresAt, deserialized.ExpiresAt);
        Assert.Equal(original.RefreshToken, deserialized.RefreshToken);
        Assert.Equal(original.RefreshTokenExpiresAt, deserialized.RefreshTokenExpiresAt);
    }

    [Fact]
    public void LoginResponse_RefreshFieldsNull_SerializaSinEsasKeys()
    {
        var response = new LoginResponse(
            AccessToken: "access-token",
            ExpiresAt: new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(response, JsonOptions);

        // System.Text.Json omits null properties by default; the
        // unchanged call sites (no refresh issuance yet) must keep the
        // wire shape stable.
        Assert.DoesNotContain("refreshToken", json);
        Assert.DoesNotContain("refreshTokenExpiresAt", json);
    }

    [Fact]
    public void LoginResponse_RefreshFieldsNoNulos_SerializaLasKeysEsperadas()
    {
        var response = new LoginResponse(
            AccessToken: "access-token",
            ExpiresAt: new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero),
            RefreshToken: "refresh-token",
            RefreshTokenExpiresAt: new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"refreshToken\":\"refresh-token\"", json);
        Assert.Contains("\"refreshTokenExpiresAt\":", json);
    }

    [Fact]
    public void AuthApiRoutes_NuevasConstantes_SonEstables()
    {
        // Lock the constants early so PR2 wiring (refresh + logout
        // endpoints + Refresh rate-limit policy name) can rely on
        // them without re-litigating the strings.
        Assert.Equal("api/v1/auth", AuthApiRoutes.Base);
        Assert.Equal("/api/v1/auth/refresh", AuthApiRoutes.Refresh);
        Assert.Equal("/api/v1/auth/logout", AuthApiRoutes.Logout);
        Assert.Equal("refresh", AuthApiRoutes.RefreshRelative);
        Assert.Equal("logout", AuthApiRoutes.LogoutRelative);
        Assert.Equal("Refresh", AuthApiRoutes.RefreshPolicyName);

        // The Base constant MUST NOT change — eight callers and the
        // existing AuthApiClient password-recovery tests rely on it.
        Assert.Equal("api/v1/auth", AuthApiRoutes.Base);
        Assert.Equal("login", AuthApiRoutes.LoginRelative);
        Assert.Equal("/api/v1/auth/login", AuthApiRoutes.Login);
    }
}
