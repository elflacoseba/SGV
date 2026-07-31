using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGV.Api.Infrastructure.Health;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Auth;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Health;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Setup;
using SGV.Web.Integration.Usuarios;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Integration.Auditoria;

[assembly: InternalsVisibleTo("SGV.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddOptions<SgvApiOptions>()
    .BindConfiguration(SgvApiOptions.SectionName)
    .Validate(options => Uri.IsWellFormedUriString(options.BaseUrl, UriKind.Absolute),
        $"{SgvApiOptions.SectionName}:BaseUrl must be an absolute URI")
    .ValidateOnStart();
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Always require HTTPS for the auth cookie outside Development. Dev keeps
        // SameAsRequest so plain-http localhost sign-in still works without TLS.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
         options.LoginPath = "/auth/sign-in";
         options.LogoutPath = "/auth/logout";
         options.AccessDeniedPath = "/error/403";
         options.Events.OnValidatePrincipal = async context =>
         {
             var revalidator = context.HttpContext.RequestServices
                 .GetRequiredService<CookiePrincipalRevalidator>();
             await revalidator.ValidateAsync(context);
         };
     });


builder.Services.AddAuthorization();

// Issue #191: cultura regional única para todo el shell web (render, model
// binding, validación, orden de strings). es-AR es la fuente de verdad para
// la presentación. El contrato HTTP wire con la API sigue siendo invariante
// (System.Text.Json default); esta cultura sólo afecta la capa de UI.
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var esAr = new System.Globalization.CultureInfo("es-AR");
    options.DefaultRequestCulture = new RequestCulture(esAr);
    options.SupportedCultures = new[] { esAr };
    options.SupportedUICultures = new[] { esAr };
    options.FallBackToParentCultures = false;
});

// HttpContextAccessor is required by ApiBearerTokenHandler so the JWT stored
// on the inbound cookie-auth ticket can be bridged into an
// `Authorization: Bearer ...` header on downstream SGV.Api calls. SGV.Api
// validates only bearer tokens (see src/SGV.Api/Program.cs), so without this
// forwarding every typed client request would land as anonymous and the API's
// [Authorize] guard would reject it.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CookiePrincipalRevalidator>();
builder.Services.AddTransient<ApiBearerTokenHandler>();

// Issue #125 (Slice 3): helper que traduce ErrorCategoria.Unauthorized en
// una redirección a /auth/sign-in con guard anti open-redirect. Scoped
// porque depende de IHttpContextAccessor (también scoped). El helper
// construye el URL destino directamente (no usa IUrlHelperFactory) para
// no acoplarse al routing context del PageModel que lo invoca.
builder.Services.AddScoped<IAuthSessionRedirector, AuthSessionRedirector>();

// Singleton: la fábrica sólo construye ClaimsPrincipal + AuthenticationProperties
// desde opciones y un access token; no carga estado mutable propio. Cada host
// (incluido cada WebApplicationFactory en la suite de tests) obtiene su propio
// snapshot de IOptions<JwtOptions> gracias al aislamiento del IServiceProvider,
// así que el reemplazo del cache estático previo por construcción por llamada
// (issue #121) es seguro y no introduce contención entre tests paralelos.
builder.Services.AddSingleton<IAuthSessionFactory, AuthSessionFactory>();

// Named HTTP client for health probe (anonymous, no bearer token).
builder.Services.AddHttpClient(SgvApiHealthProbeHttpClient.Name, (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddHttpClient(CookiePrincipalRevalidator.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<SgvApiOptions>>()
        .Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient(AuthApiClient.AuthenticatedHttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s budget for a login request. The HttpClient default (100s) is too long:
    // the user is staring at a sign-in form and a hung page is indistinguishable
    // from a server-side crash. A bounded budget converts transport stalls into
    // TaskCanceledException, which SignInModel.OnPostAsync handles as recoverable.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient(AuthApiClient.AnonymousHttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // Password recovery is explicitly anonymous. This client intentionally has
    // no ApiBearerTokenHandler in its pipeline.
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddTransient<IAuthApiClient>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    return new AuthApiClient(
        factory.CreateClient(AuthApiClient.AuthenticatedHttpClientName),
        factory.CreateClient(AuthApiClient.AnonymousHttpClientName));
});

builder.Services.AddHttpClient<IUnidadOrganizativaApiClient, UnidadOrganizativaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s budget paralelo a AuthApiClient: las consultas de unidades organizativas
    // (listado, árbol, dropdowns) deben acotarse a un tiempo predecible para que
    // los fallos de transporte se traduzcan en errores recuperables en la UI.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<ICargoApiClient, CargoApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s budget for a Create-form request. The HttpClient default (100s) is
    // too long: the user is staring at a submit button and a hung page is
    // indistinguishable from a server-side crash. A bounded budget converts
    // transport stalls into TaskCanceledException, which CreateModel.OnPostAsync
    // already handles as a recoverable error.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IPuestosApiClient, PuestosApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s budget, paralelo a CargoApiClient y HabilidadApiClient: el usuario
    // espera ver el form/listado cargado y un timeout prolongado se confunde
    // con un crash de servidor. TaskCanceledException se traduce en error
    // recuperable en IndexModel/CreateModel/EditModel.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IHabilidadApiClient, HabilidadApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // Mismo budget que CargoApiClient: 10s para que la espera del usuario sea
    // acotada y los fallos de transporte se traduzcan en errores recuperables.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<ICategoriaHabilidadApiClient, CategoriaHabilidadApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // Mismo budget que el resto de los clientes tipados: 10s para que
    // los dropdowns poblados por este catálogo fallen rápido y la UI
    // muestre feedback antes de que el usuario confirme el form.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IPersonaApiClient, PersonaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s paralelo a Cargo/Habilidad/Puesto: el listado paginado de
    // personas y los formularios de Create/Edit no pueden esperar el
    // HttpClient default (100s); un timeout largo se confunde con un
    // crash de servidor y TaskCanceledException debe traducirse a un
    // error recuperable en la Razor Page (PR #3 lo rendereará con el
    // banner estándar de Transporte).
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

// Issue #208 / Slice 2: cliente HTTP tipado del módulo Ocupaciones. La
// superficie actual es read-only (ListarAsync, ObtenerPorIdAsync); las
// mutaciones Crear/Actualizar/Finalizar/Eliminar/Reactivar llegan en
// Slice 3a. Mismo budget (10s) y bearer pipeline que el resto de los
// clientes administrativos para mantener consistencia de fallos
// recuperables en la Razor Page.
builder.Services.AddHttpClient<IOcupacionApiClient, OcupacionApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IVacanteApiClient, VacanteApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IUsuarioApiClient, UsuarioApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s paralelo al resto del shell web: el listado paginado de
    // usuarios y los formularios de Create/Edit/Details (PR 3/4) no
    // pueden esperar el HttpClient default (100s). El timeout acotado
    // convierte TaskCanceledException en feedback recuperable vía
    // TransportFailureClassifier, análogo a Persona/Cargo/Habilidad.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

// Slice 3 del change `implementa-modulo-auditorias`: cliente HTTP
// tipado del listado admin-only de auditoría. Acceso restringido al
// rol Administrador (D-1); el backend exige `[Authorize(Roles =
// RolesSgv.Administrador)]` en el controller S2, así que el bearer
// del cookie de sesión debe adjuntarse en cada request saliente
// (mismo patrón que el resto de los clientes tipados). 10s budget
// paralelo a Puestos/Ocupación: el listado paginado de auditoría
// no puede esperar el HttpClient default (100s); un timeout largo
// se confunde con un crash y TaskCanceledException debe traducirse
// a feedback recuperable vía TransportFailureClassifier en la
// Razor Page.
builder.Services.AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

// Setup inicial one-time del primer Administrador (issue #195 / WU-4
// del change `setup-admin-inicial-issue-195`). El cliente es
// explícitamente anónimo: NO usa ApiBearerTokenHandler porque los
// endpoints /api/v1/setup y /api/v1/tipos-documento están
// [AllowAnonymous] desde el PR #1 (chicken-and-egg: el primer admin
// no puede autenticarse si todavía no existe). El cache de status
// vive en IMemoryCache, registrado por
// `builder.Services.AddMemoryCache()` justo debajo.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ISetupApiClient, SetupApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s paralelo a AuthApiClient: el status y el submit del setup
    // no pueden esperar el HttpClient default (100s); un timeout
    // largo se confunde con un crash y TaskCanceledException debe
    // traducirse a un mensaje recuperable en la Razor Page.
    client.Timeout = TimeSpan.FromSeconds(10);
});
// NO `.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())`:
// los endpoints de setup son [AllowAnonymous].

// Health checks — upstream probe and response writer
builder.Services.AddHealthChecks()
    .AddCheck<SgvApiUpstreamHealthCheck>("sgv-api-upstream", tags: new[] { "ready" });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

// BFF same-origin para el buscador modal. Mantiene el JWT en el servidor:
// el navegador usa la cookie Web y el cliente tipado reenvía el bearer a API.
const int SearchMaxLength = 200;

// Mantener sincronizado con PersonaRepository.ApplySort.
HashSet<string> allowedSorts = new(StringComparer.OrdinalIgnoreCase)
{
    "apellidos_asc", "apellidos_desc",
    "nombres_asc", "nombres_desc",
    "legajo_asc", "legajo_desc",
    "email_asc", "email_desc",
    "documento_asc", "documento_desc",
};

HashSet<string> allowedSegmentos = new(StringComparer.OrdinalIgnoreCase)
{
    "activas", "eliminadas",
};

app.MapGet("/api/v1/personas/consulta", async (
    HttpContext httpContext,
    int p,
    int pageSize,
    string? search,
    string? sort,
    string? segmento,
    bool? soloSinUsuario,
    IPersonaApiClient personaApiClient,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("SGV.Web.Personas.BffUpstream");

    if (!string.IsNullOrEmpty(search) && Encoding.UTF8.GetByteCount(search) > SearchMaxLength)
    {
        return Results.Problem(
            title: "Parámetro 'search' fuera de rango",
            detail: $"El parámetro 'search' excede el límite de {SearchMaxLength} bytes (UTF-8).",
            statusCode: StatusCodes.Status400BadRequest);
    }

    string resolvedSort = string.IsNullOrWhiteSpace(sort) ? "apellidos_asc" : sort.Trim();
    if (!allowedSorts.Contains(resolvedSort))
    {
        return Results.Problem(
            title: "Parámetro 'sort' inválido",
            detail: $"El parámetro 'sort' debe ser uno de: {string.Join(", ", allowedSorts.OrderBy(s => s))}.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    PersonaSegmentoListado resolvedSegmento = PersonaSegmentoListado.Activas;
    if (!string.IsNullOrWhiteSpace(segmento))
    {
        if (!allowedSegmentos.Contains(segmento))
        {
            return Results.Problem(
                title: "Parámetro 'segmento' inválido",
                detail: $"El parámetro 'segmento' debe ser uno de: {string.Join(", ", allowedSegmentos)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        resolvedSegmento = segmento.Equals("eliminadas", StringComparison.OrdinalIgnoreCase)
            ? PersonaSegmentoListado.Eliminadas
            : PersonaSegmentoListado.Activas;
    }

    var query = new PersonaListQuery(
        Page: Math.Max(1, p),
        PageSize: Math.Clamp(pageSize, 1, 100),
        Search: search,
        Sort: resolvedSort,
        Segmento: resolvedSegmento,
        SoloSinUsuario: soloSinUsuario);

    try
    {
        var result = await personaApiClient.QueryAsync(query, cancellationToken);
        return Results.Ok(result);
    }
    catch (HttpRequestException ex)
    {
        return PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, ex, clientCancelled: cancellationToken.IsCancellationRequested);
    }
    catch (TaskCanceledException ex)
    {
        return PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, ex, clientCancelled: cancellationToken.IsCancellationRequested);
    }
}).RequireAuthorization();

// Health check endpoints — anonymous, no auth required.
// /health/live responds 200 unconditionally (process is alive).
// /health/ready probes the SGV API upstream via SgvApiUpstreamHealthCheck.
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

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
