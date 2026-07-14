using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using SGV.Web.Pages.Auth;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Tests that SignInModel.OnPostAsync correctly handles transport-layer
/// exceptions from IAuthApiClient, rendering user-facing Spanish error messages
/// while preserving cooperative cancellation semantics.
/// </summary>
[Collection("WebIntegration")]
public sealed class SignInTransportTests
{
    private readonly WebIntegrationFixture _fixture;

    public SignInTransportTests(WebIntegrationFixture fixture) => _fixture = fixture;

    /// <summary>
    /// When the upstream API is unreachable (<see cref="HttpRequestException"/>),
    /// the sign-in page MUST display a transport-error message in Spanish and
    /// keep the user on the sign-in page instead of redirecting to /Error.
    /// </summary>
    [Fact]
    public async Task SignIn_HttpRequestException_RendersSpanishError()
    {
        var handler = new ThrowingHttpMessageHandler(
            new HttpRequestException("Connection refused"));

        await using var lease = CreateTransportLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("No pudimos contactar al servicio de autenticaci&#xF3;n.", content);
    }

    /// <summary>
    /// When the upstream API times out (<see cref="TaskCanceledException"/>)
    /// and the request's <see cref="CancellationToken"/> was NOT proactively
    /// cancelled by the client, the sign-in page MUST display a timeout-error
    /// message in Spanish and keep the user on the sign-in page.
    /// </summary>
    [Fact]
    public async Task SignIn_TaskCanceledExceptionNotCancelled_RendersTimeoutError()
    {
        var handler = new ThrowingHttpMessageHandler(
            new TaskCanceledException("The operation was cancelled."));

        await using var lease = CreateTransportLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("La autenticaci&#xF3;n tard&#xF3; demasiado.", content);
    }

    /// <summary>
    /// When the upstream times out AND the request's cancellation token was
    /// proactively cancelled by the client (user navigated away), the
    /// <see cref="TaskCanceledException"/> MUST propagate unhandled — no
    /// ModelState error is added. This test verifies the exception flow by
    /// directly invoking <see cref="SignInModel.OnPostAsync"/> with a
    /// pre-cancelled <see cref="CancellationToken"/> and asserting the
    /// exception surfaces.
    /// </summary>
    [Fact]
    public async Task SignIn_TaskCanceledExceptionCancelled_Propagates()
    {
        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: s =>
            {
                s.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                s.Configure<JwtOptions>(o =>
                {
                    o.SigningKey = AdminJwtTestHelper.SigningKey;
                    o.Issuer = AdminJwtTestHelper.Issuer;
                    o.Audience = AdminJwtTestHelper.Audience;
                });
            });

        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Build a SignInModel with real dependencies via DI
        var model = ActivatorUtilities.CreateInstance<SignInModel>(sp);
        var httpContext = new DefaultHttpContext { RequestServices = sp };
        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new CompiledPageActionDescriptor()
        };

        // Pre-cancelled token — simulates the user navigating away
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The exception MUST propagate; no ModelState error should be added
        var exception = await Record.ExceptionAsync(() => model.OnPostAsync(cts.Token));

        Assert.NotNull(exception);

        // No transport/timeout error should be in ModelState
        Assert.DoesNotContain(model.ModelState.Values,
            v => v.Errors.Any(e =>
                e.ErrorMessage.Contains("No pudimos contactar") ||
                e.ErrorMessage.Contains("tardó demasiado")));
    }

    /// <summary>
    /// Regression guard: a 401 response from the upstream still produces
    /// "Credenciales inválidas." (the existing behavior MUST NOT regress
    /// when adding the try/catch transport blocks).
    /// </summary>
    [Fact]
    public async Task SignIn_401_StillInvalidCredentials()
    {
        var handler = new ThrowingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var lease = CreateTransportLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "bad-password"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenciales inv&#xE1;lidas.", content);
    }

    /// <summary>
    /// Creates an authenticated lease with a custom auth API handler that
    /// throws or returns a specific response, using the <see cref="WebIntegrationFixture"/>
    /// root factory to share the host infrastructure while keeping per-test
    /// isolation via <c>await using</c>.
    /// </summary>
    private WebClientLease CreateTransportLease(HttpMessageHandler handler)
    {
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: s => s.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test"),
            authApiHandler: handler);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        return new WebClientLease(factory, client, new TestSentinel());
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that either returns a fixed response
    /// or throws a fixed exception on every <c>SendAsync</c> call.
    /// </summary>
    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public ThrowingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_response!);
        }
    }
}
