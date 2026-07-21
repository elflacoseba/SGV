using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Integration tests for the password recovery endpoints exposed by
/// <c>AuthController</c> (issue #181). Each test derives its own
/// factory via <see cref="ApiWebApplicationFactory.WithOverrides"/>
/// and injects a <see cref="FakePasswordResetService"/> so the
/// recovery flow stays under test control. Rate-limit assertions
/// use a fresh bucket per test (the named policy's fixed window
/// starts when the host is built).
/// </summary>
[Collection("ApiIntegration")]
public sealed class AuthControllerPasswordResetTests
{
    private readonly ApiIntegrationFixture _fixture;

    public AuthControllerPasswordResetTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    private async Task<ApiWebApplicationFactory> BuildFactoryAsync(IPasswordResetService? fake = null)
    {
        var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPasswordResetService>();
            services.AddSingleton<IPasswordResetService>(fake ?? new FakePasswordResetService());
        });
        await Task.CompletedTask;
        return factory;
    }

    [Fact]
    public async Task ForgotPassword_NoAuthHeader_Returns200()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_KnownAndUnknownIdentifiers_ReturnByteEquivalentBodies()
    {
        // Anti-enumeration: response MUST be byte-equivalent for
        // known and unknown identifiers. The fake service answers
        // Success in both branches (the production service swallows
        // UserNotFound internally); the controller then paints the
        // exact same payload.
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var known = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("admin"));
        var unknown = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("ghost@nowhere.invalid"));

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        var knownBody = await known.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        Assert.Equal(knownBody, unknownBody);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400WithSpanishMessage()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(UserId: "user-1", Token: "bogus", NewPassword: "Password1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("restablecimiento", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_Returns200()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(UserId: "user-1", Token: "valid", NewPassword: "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_PolicyViolation_Returns400()
    {
        // The validator MUST reject a too-short password before the
        // endpoint hits the service.
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(UserId: "user-1", Token: "valid", NewPassword: "Ab1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_FourthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader()
    {
        // Permit limit is 3 requests per 15 minutes for the
        // "ForgotPassword" policy. The client sends from the same
        // loopback IP, so all 4 requests share the bucket.
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password",
                new ForgotPasswordRequest("admin"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("admin"));

        Assert.Equal(
            (HttpStatusCode)StatusCodes.Status429TooManyRequests,
            blocked.StatusCode);
        // Retry-After MUST be set so polite clients can back off.
        Assert.True(blocked.Headers.Contains("Retry-After"),
            "Expected Retry-After header on rejected request.");
    }
}
