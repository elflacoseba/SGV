using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SGV.Aplicacion;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Seguridad;
using SGV.Infraestructura;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Api.Seguridad;

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
builder.Services.AddScoped<AuditoriaSaveChangesInterceptor>();
var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");
builder.Services.AddDbContext<SgvDbContext>((sp, options) =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
});

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes")
    .ValidateOnStart();

builder.Services
    .AddIdentityCore<SgvIdentityUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<SgvDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(_ => { });
builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerFromJwtOptions>();
builder.Services.AddAuthorization(opts =>
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Anonymous / system user for audit trail
builder.Services.AddScoped<IUsuarioActual, UsuarioActualAnonimo>();

// Application services (validators, command/query services)
builder.Services.AddAplicacionServicios();

// Infrastructure services (repositories, UoW, query services)
builder.Services.AddInfraestructuraServicios();

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

// Middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
