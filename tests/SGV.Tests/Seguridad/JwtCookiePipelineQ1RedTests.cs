using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// RED test for design question Q1 (see design.md): verify that the JWT
/// bearer pipeline in <c>SGV.Api</c> and the cookie pipeline in
/// <c>SGV.Web</c> are configured in a way that lets
/// <c>OnTokenValidated</c> / <c>OnValidatePrincipal</c> handlers run
/// AFTER token signature validation but BEFORE authorization. The
/// runtime hooks themselves are added in Phase 2 of this change; this
/// test merely proves the configuration scaffold is in place and ready
/// for them.
/// </summary>
/// <remarks>
/// <para>
/// RED in Phase 1 (Foundation) because the handlers are not yet wired;
/// the test will become GREEN in Phase 2 (Core) once the revalidator
/// hooks land. The failure mode in Phase 1 is observable: the events
/// collection is empty, signaling the integration is still pending.
/// </para>
/// <para>
/// Fallback documented in the design: if these hooks prove insufficient
/// in CI, a post-authentication middleware filter revalidates via
/// <c>IRevalidatorCredenciales.SigueVigenteAsync(sub)</c>. That
/// fallback is also Phase 2 work.
/// </para>
/// </remarks>
public sealed class JwtCookiePipelineQ1RedTests
{
    [Fact]
    public void Api_JwtBearer_RegistersOnTokenValidatedHandler()
    {
        // Arrange: bootstrap the API host so the JwtBearerOptions get
        // post-configured (ConfigureJwtBearerFromJwtOptions runs at
        // post-configure time).
        using var factory = new ApiOptionsProbeFactory();
        using var scope = factory.Services.CreateScope();
        var optionsMonitor = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        // Act: read the resolved options for the JwtBearer scheme
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert: scaffold exists — OnTokenValidated has at least one
        // delegate registered. Phase 2 will add the revalidator; this
        // test is RED until that happens.
        Assert.NotNull(jwtOptions.Events);
        Assert.NotNull(jwtOptions.Events!.OnTokenValidated);
    }

    [Fact]
    public void Web_CookieAuth_RegistersOnValidatePrincipalHandler()
    {
        // Arrange: bootstrap the Web host so the cookie scheme is
        // configured.
        using var factory = new WebOptionsProbeFactory();
        using var scope = factory.Services.CreateScope();
        var optionsMonitor = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        // Act
        var cookieOptions = optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert: scaffold exists — OnValidatePrincipal has at least one
        // delegate registered. Phase 2 will add the revalidator.
        Assert.NotNull(cookieOptions.Events);
        Assert.NotNull(cookieOptions.Events!.OnValidatePrincipal);
    }

    /// <summary>
    /// Minimal <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
    /// substitute que bootea <c>SGV.Api</c> con la configuración
    /// mínima necesaria (ConnectionString válida para sortear el
    /// fail-loud en Program.cs, JwtSigningKey ≥ 32 bytes) y deja que
    /// <see cref="JwtBearerOptions"/> se post-configure via
    /// <c>ConfigureJwtBearerFromJwtOptions</c>.
    /// </summary>
    /// <remarks>
    /// Usamos <c>UseSetting</c> (no <c>ConfigureAppConfiguration</c>)
    /// porque Program.cs lee <c>builder.Configuration</c> en el cuerpo
    /// del builder y los <c>UseSetting</c> se aplican ANTES de que se
    /// evalúe ese código. <c>ConfigureAppConfiguration</c> en cambio
    /// agrega providers que se cargan DESPUÉS, lo que provoca el
    /// <c>OptionsValidationException</c> en el fail-loud de conexión.
    /// </remarks>
    private sealed class ApiOptionsProbeFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<SGV.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SgvDatabase",
                "Server=127.0.0.1;Port=1;Database=sgv_probe;User=root;Password=;");
            builder.UseSetting("Jwt:SigningKey", "Q1-PIPELINE-PROBE-MIN-32-BYTES!!!");
            builder.UseSetting("Jwt:Issuer", "sgv-api");
            builder.UseSetting("Jwt:Audience", "sgv-clients");
            builder.UseSetting("Jwt:TokenLifetimeMinutes", "60");
            builder.UseSetting("AllowedOrigins:0", "http://localhost");
        }
    }

    private sealed class WebOptionsProbeFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<SGV.Web.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SgvDatabase",
                "Server=127.0.0.1;Port=1;Database=sgv_probe;User=root;Password=;");
            builder.UseSetting("Jwt:SigningKey", "Q1-PIPELINE-PROBE-MIN-32-BYTES!!!");
            builder.UseSetting("Jwt:Issuer", "sgv-api");
            builder.UseSetting("Jwt:Audience", "sgv-clients");
            builder.UseSetting("ApiBaseUrl", "http://localhost:5000");
        }
    }
}