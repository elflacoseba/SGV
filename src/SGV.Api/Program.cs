using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SGV.Aplicacion;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Setup;
using SGV.Infraestructura;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Api.Infrastructure.Health;
using SGV.Api.Seguridad;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Controllers and problem details
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "SGV API",
        Version = "v1",
        Description = "HTTP API for SGV organizational structure, skills data, personas management, and organizational-unit management."
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

});

// MySQL DbContext with audit interceptor
var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");

// Validate connection string at startup — fail-loud before AutoDetect or first request.
// This must run before AddDbContext so the validator is registered first,
// allowing ValidateOnStart to trigger on Build() for the tests.
if (string.IsNullOrWhiteSpace(connectionString))
    throw new OptionsValidationException(
        nameof(DbContextOptions<SgvDbContext>),
        typeof(DbContextOptions<SgvDbContext>),
        ["Debe configurar ConnectionStrings:SgvDatabase antes de iniciar la API."]);
if (!connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
    || !connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase))
    throw new OptionsValidationException(
        nameof(DbContextOptions<SgvDbContext>),
        typeof(DbContextOptions<SgvDbContext>),
        ["ConnectionStrings:SgvDatabase inválida: debe incluir Server= y Database=."]);

// Versión de servidor MySQL externalizable vía config (default 8.0.36).
// Esto evita ServerVersion.AutoDetect, que abre una conexión TCP al construir
// las opciones del DbContext y bloquea el primer request autenticado cuando
// MySQL está transitoriamente inalcanzable. Ver docs/decisiones-implementacion.md §6.
var rawServerVersion = builder.Configuration["MySql:ServerVersion"] ?? "8.0.36";
if (!Version.TryParse(rawServerVersion, out var serverVersionNumber)
    || serverVersionNumber.Major <= 0)
    throw new OptionsValidationException(
        nameof(DbContextOptions<SgvDbContext>),
        typeof(DbContextOptions<SgvDbContext>),
        [$"MySql:ServerVersion '{rawServerVersion}' inválida: debe ser un Version parseable (ej. 8.0.36)."]);
var serverVersion = new MySqlServerVersion(serverVersionNumber);

builder.Services.AddScoped<AuditoriaSaveChangesInterceptor>();
builder.Services.AddDbContext<SgvDbContext>((sp, options) =>
{
    // CommandTimeout(30) is paired with the shell web's 30s HttpClient.Timeout
    // to absorb the realistic latency of the SGV.Api ← Tailscale ← satellite
    // link: a single MySqlConnection handshake + auth + TLS already exceeds 2s,
    // and any non-trivial Razor Page request stacks 2-3 sequential HTTP calls.
    // Without this, the per-command default (30s) plus the HttpClient default
    // (100s) still races the propagated client cancellation once the upstream
    // path gets slow. Configured here so every DbContext in the API inherits it.
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
            mySqlOptions.CommandTimeout(30))
           .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
});

// Connection string — register the IValidateOptions for the warn-on-missing-timeout case.
// Hard failures (null, whitespace, missing Server= or Database=) are thrown inline above.
// The health check uses a raw MySqlConnection and does NOT trigger ServerVersion.AutoDetect.
builder.Services.AddSingleton<IValidateOptions<DbContextOptions<SgvDbContext>>,
    SgvDbContextOptionsValidator>();
builder.Services.AddOptions<DbContextOptions<SgvDbContext>>()
    .Validate(_ => true, "noop")
    .ValidateOnStart();

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes")
    .ValidateOnStart();

// SmtpOptions is required for the password reset flow (issue #181).
// Outside Development the host fails loud when WebBaseUrl is missing
// or not an absolute URL; the integration tests rely on the in-memory
// factory overriding ASPNETCORE_ENVIRONMENT.
builder.Services
    .AddOptions<SmtpOptions>()
    .BindConfiguration(SmtpOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddIdentityCore<SgvIdentityUser>(options =>
    {
        // Password policy lives in SGV.Contracts.Seguridad.PasswordPolicy
        // (single source of truth) — keep this block in sync with the
        // FluentValidation rules in ChangePasswordRequestValidator /
        // ResetPasswordRequestValidator and the Razor Pages pre-flight
        // checks in CambiarContrasena / ResetPassword.
        options.Password.RequireDigit = PasswordPolicy.RequireDigit;
        options.Password.RequireLowercase = PasswordPolicy.RequireLowercase;
        options.Password.RequireUppercase = PasswordPolicy.RequireUppercase;
        options.Password.RequireNonAlphanumeric = PasswordPolicy.RequireNonAlphanumeric;
        options.Password.RequiredLength = PasswordPolicy.MinLength;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<SgvDbContext>()
    .AddDefaultTokenProviders();

// Password reset tokens must expire after one hour. Identity stores the
// lifespan on DataProtectionTokenProviderOptions, not on
// IdentityOptions.Tokens. The reset link in the email must reach the
// user well before this window closes.
builder.Services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(_ => { });
builder.Services.AddSingleton<IRevalidatorCredenciales, RevalidatorCredenciales>();
builder.Services.AddOptions<JwtBearerOptions>()
    .Configure<IRevalidatorCredenciales>((options, revalidator) =>
    {
        var existingHandler = options.Events?.OnTokenValidated;
        options.Events ??= new JwtBearerEvents();
        options.Events.OnTokenValidated = async context =>
        {
            if (existingHandler is not null)
            {
                await existingHandler(context);
            }

            var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(subject))
            {
                context.Fail("subject claim required");
                return;
            }

            var isValid = await revalidator.SigueVigenteAsync(
                subject,
                context.HttpContext.RequestAborted);
            context.HttpContext.Items[RevalidatorCredenciales.ValidationMarker] = true;
            if (!isValid)
            {
                context.Fail("Credencial revocada o cuenta bloqueada.");
            }
        };
    });
builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerFromJwtOptions>();
builder.Services.AddAuthorization(opts =>
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Health checks — liveness and readiness probes
builder.Services.AddHealthChecks()
    .AddCheck<SgvDbContextReadinessHealthCheck>("mysql", tags: new[] { "ready" });

// Current authenticated user for audit trails and self-mutation guards.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttpContext>();

// Application services (validators, command/query services)
builder.Services.AddAplicacionServicios();

// Infrastructure services (repositories, UoW, query services)
builder.Services.AddInfraestructuraServicios();

// Issue #191: cultura regional única para la API. es-AR por defecto para que
// los servicios que dependan de CultureInfo.CurrentCulture (por ejemplo,
// StringComparer.Create(CultureInfo.CurrentCulture, ...) en el orden de
// unidades organizativas) operen en una sola cultura coherente con el shell
// web. El contrato HTTP wire es invariante (System.Text.Json default) — esta
// cultura sólo afecta orden de strings y formateo a nivel proceso.
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var esAr = new System.Globalization.CultureInfo("es-AR");
    options.DefaultRequestCulture = new RequestCulture(esAr);
    options.SupportedCultures = new[] { esAr };
    options.SupportedUICultures = new[] { esAr };
    options.FallBackToParentCultures = false;
});

// Rate limiting (issue #181): two named fixed-window policies for the
// password recovery endpoints. Both are applied BEFORE authentication
// (see pipeline order below) so anonymous bursts are still subject to
// quota. The OnRejected callback writes Retry-After so polite clients
// can back off without polling.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(AuthApiRoutes.ForgotPasswordPolicyName, policy =>
    {
        policy.PermitLimit = 3;
        policy.Window = TimeSpan.FromMinutes(15);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter(AuthApiRoutes.ResetPasswordPolicyName, policy =>
    {
        policy.PermitLimit = 5;
        policy.Window = TimeSpan.FromMinutes(15);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    // Issue #195: rate limiting para el setup one-time del primer
    // Administrador. 5 req / 15 min: más permisivo que ForgotPassword
    // (3) porque el formulario tiene 9 campos con probabilidad alta
    // de error humano, y más estricto que ResetPassword (5) en la
    // misma ventana porque crea un admin.
    options.AddFixedWindowLimiter(SetupApiRoutes.SetupPolicyName, policy =>
    {
        policy.PermitLimit = 5;
        policy.Window = TimeSpan.FromMinutes(15);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter(AuthApiRoutes.ChangePasswordPolicyName, policy =>
    {
        policy.PermitLimit = 5;
        policy.Window = TimeSpan.FromMinutes(15);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = static (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            // Fall back to the policy window length when ASP.NET does not
            // surface explicit RetryAfter metadata (older middleware
            // behaviour). The headers stay aligned with the policy.
            context.HttpContext.Response.Headers.RetryAfter = "900";
        }
        return ValueTask.CompletedTask;
    };
});

// CORS: allow web app origin in development; fail loud if unconfigured outside Development.
// The AllowedOrigins read happens inside the AddDefaultPolicy callback so it observes the
// post-Build configuration (including any ConfigureAppConfiguration overrides applied by
// WebApplicationFactory in tests). The InvalidOperationException surfaces immediately when
// CorsService resolves IOptions<CorsOptions> at host start, before the first request.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "SGV.Api: la sección de configuración 'AllowedOrigins' es obligatoria " +
                    "fuera del ambiente Development. Configure AllowedOrigins__0, " +
                    "AllowedOrigins__1, ... vía variables de entorno.");
            }

            // Development-only fallback: any origin is allowed but credentials stay off.
            // The wildcard origin must never be combined with AllowCredentials (browsers
            // reject that combination, and ASPIl's behaviour around it is not safe to rely
            // on). Dev has no real session to exfiltrate, so this fallback is safe.
            // See spec api-cors-allowed-origins-validation for the constraint.
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowCredentials();
        }
    });
});

var app = builder.Build();

// Issue #277: diagnóstico de integridad jerárquica al arranque. Un fallo
// del diagnóstico NO debe abortar el startup (la app puede operar y la
// corrección puede ser operativa vía el script SQL); sólo se registra
// un WARNING. La detección corre en su propio scope manual porque
// IDiagnosticoJerarquiaService depende de SgvDbContext (scoped) y la
// invocación ocurre fuera del request pipeline.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var diagnostico = scope.ServiceProvider
                .GetRequiredService<IDiagnosticoJerarquiaService>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILogger<Program>>();
            var ciclos = await diagnostico.DiagnosticarAsync();

            if (ciclos.Count == 0)
            {
                logger.LogInformation(
                    "SGV: diagnóstico de jerarquía OK (sin ciclos detectados en UnidadesOrganizativas).");
                return;
            }

            foreach (var ciclo in ciclos)
            {
                var idsCsv = string.Join(", ", ciclo.Nodos.Select(n => n.ToString()));
                logger.LogWarning(
                    "SGV: ciclo jerárquico detectado en UnidadesOrganizativas. Nodos participantes: {Nodos}",
                    idsCsv);
            }
        }
        catch (Exception ex)
        {
            // Fallo del diagnóstico no debe abortar el arranque. La
            // corrección es operativa (script SQL idempotente).
            var bootstrapLogger = app.Services
                .GetRequiredService<ILogger<Program>>();
            bootstrapLogger.LogWarning(
                ex,
                "SGV: el diagnóstico de jerarquía falló en el arranque y fue ignorado.");
        }
    });
});

// Middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseRequestLocalization();

// Issue #181: rate limiter MUST run before authentication so the
// "ForgotPassword"/"ResetPassword" named policies can throttle
// anonymous bursts before the auth pipeline short-circuits. See the
// composition comments in AddRateLimiter for the per-policy limits.
app.UseRateLimiter();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && !context.Items.ContainsKey(RevalidatorCredenciales.ValidationMarker))
    {
        // Real bearer principals carry `iss` (issuer) once MapInboundClaims
        // has run; the Test auth scheme and similar stubs do not. This lets
        // the revalidator run on production JWT without affecting test or
        // non-bearer pipelines.
        var hasIssuer = context.User.HasClaim(c => c.Type == JwtRegisteredClaimNames.Iss);
        if (!hasIssuer)
        {
            await next();
            return;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(subject))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var revalidator = context.RequestServices
            .GetRequiredService<IRevalidatorCredenciales>();
        var isValid = await revalidator.SigueVigenteAsync(
            subject,
            context.RequestAborted);
        context.Items[RevalidatorCredenciales.ValidationMarker] = true;
        if (!isValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});
app.UseAuthorization();

// Health check endpoints — anonymous and tag-based.
// /health/live responds 200 unconditionally (process is alive).
// /health/ready probes MySQL via SgvDbContextReadinessHealthCheck.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();

app.MapControllers();

app.Run();
