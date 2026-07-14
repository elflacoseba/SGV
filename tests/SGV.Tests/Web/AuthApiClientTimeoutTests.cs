using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Tests that IAuthApiClient and IUnidadOrganizativaApiClient have a 10-second
/// HttpClient.Timeout configured (matching the existing Cargo/Puestos/Habilidad
/// clients), and that a slow upstream correctly produces TaskCanceledException.
/// </summary>
public sealed class AuthApiClientTimeoutTests
{
    /// <summary>
    /// Builds a factory pre-configured with SgvApi:BaseUrl and Jwt settings.
    /// Uses ConfigureAppConfiguration + ConfigureTestServices to ensure
    /// both the IConfiguration binding and the options pipeline are set up.
    /// </summary>
    private static WebApplicationFactory<SGV.Web.Program> CreateFactory()
    {
        return new WebApplicationFactory<SGV.Web.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SgvApi:BaseUrl"] = "https://api.test",
                        ["Jwt:SigningKey"] = AdminJwtTestHelper.SigningKey,
                        ["Jwt:Issuer"] = AdminJwtTestHelper.Issuer,
                        ["Jwt:Audience"] = AdminJwtTestHelper.Audience,
                    });
                });
            });
    }

    /// <summary>
    /// Verifies that the real DI registration in Program.cs sets
    /// <see cref="HttpClient.Timeout"/> to exactly 10 seconds for
    /// <see cref="IAuthApiClient"/>. Uses reflection to read the private
    /// HttpClient field on the typed client since there is no public accessor.
    /// </summary>
    [Fact]
    public void AuthApiClient_HasTenSecondTimeout()
    {
        using var app = CreateFactory();
        using var scope = app.Services.CreateScope();

        var typedClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();
        var httpClient = ExtractHttpClient(typedClient);

        Assert.Equal(TimeSpan.FromSeconds(10), httpClient.Timeout);
    }

    /// <summary>
    /// Verifies that the real DI registration in Program.cs sets
    /// <see cref="HttpClient.Timeout"/> to exactly 10 seconds for
    /// <see cref="IUnidadOrganizativaApiClient"/>.
    /// </summary>
    [Fact]
    public void UnidadOrganizativaApiClient_HasTenSecondTimeout()
    {
        using var app = CreateFactory();
        using var scope = app.Services.CreateScope();

        var typedClient = scope.ServiceProvider.GetRequiredService<IUnidadOrganizativaApiClient>();
        var httpClient = ExtractHttpClient(typedClient);

        Assert.Equal(TimeSpan.FromSeconds(10), httpClient.Timeout);
    }

    /// <summary>
    /// Extracts the private HttpClient field from a typed client created
    /// via AddHttpClient&lt;TInterface, TImplementation&gt; in the minimal API host.
    /// Tries multiple known field names for C# primary-constructor captured fields,
    /// and falls back to enumerating all private fields of HttpClient type.
    /// </summary>
    private static HttpClient ExtractHttpClient<TClient>(TClient typedClient) where TClient : class
    {
        var type = typedClient.GetType();

        // Try known field names for primary constructor parameters
        // C# 12+ primary constructors in non-record classes synthesize a
        // field named <paramName>P (verified via reflection: "<httpClient>P").
        var field = type.GetField("<httpClient>P", BindingFlags.Instance | BindingFlags.NonPublic);
        field ??= type.GetField("httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        field ??= type.GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);

        // Fallback: find any private HttpClient-typed field
        if (field is null)
        {
            var allFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            // Write field names for debugging
            var names = string.Join(", ", allFields.Select(f => $"{f.Name}:{f.FieldType.Name}"));
            throw new InvalidOperationException(
                $"Could not find HttpClient field. Available fields: [{names}]");
        }

        return (HttpClient)field.GetValue(typedClient)!;
    }

    /// <summary>
    /// When the upstream never responds (simulated via a handler that awaits
    /// a TaskCompletionSource), the <see cref="IAuthApiClient.LoginAsync"/>
    /// call MUST throw <see cref="TaskCanceledException"/> when the
    /// cancellation token fires. This verifies the deterministic cancellation
    /// contract that the SignInModel's catch blocks depend on.
    /// </summary>
    [Fact]
    public async Task Login_SlowUpstream_TaskCanceledBeforeTimeout()
    {
        var upstreamTcs = new TaskCompletionSource();

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: s => s.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test"),
            authApiHandler: new SlowUpstreamHandler(upstreamTcs));

        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var loginTask = client.LoginAsync(new LoginRequest("x", "y"), cts.Token);

        var exception = await Record.ExceptionAsync(() => loginTask);

        try
        {
            Assert.IsType<TaskCanceledException>(exception);
        }
        finally
        {
            // Signal the handler to complete so the test doesn't hang on dispose.
            upstreamTcs.TrySetCanceled();
        }
    }

    /// <summary>
    /// Simulates an upstream that never completes — awaits a
    /// <see cref="TaskCompletionSource"/> indefinitely.
    /// When the cancellation token fires (via CTS timeout), the handler
    /// registers a callback on the token to cancel the TCS, then rethrows
    /// as <see cref="TaskCanceledException"/>.
    /// </summary>
    private sealed class SlowUpstreamHandler(TaskCompletionSource signal) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var _ = cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetCanceled(),
                signal);

            try
            {
                await signal.Task;
            }
            catch (OperationCanceledException)
            {
                throw new TaskCanceledException(
                    "The upstream did not respond and the operation was cancelled.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
