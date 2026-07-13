using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;

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
    });

builder.Services.AddAuthorization();

// HttpContextAccessor is required by ApiBearerTokenHandler so the JWT stored
// on the inbound cookie-auth ticket can be bridged into an
// `Authorization: Bearer ...` header on downstream SGV.Api calls. SGV.Api
// validates only bearer tokens (see src/SGV.Api/Program.cs), so without this
// forwarding every typed client request would land as anonymous and the API's
// [Authorize] guard would reject it.
builder.Services.AddHttpContextAccessor();
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

builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

builder.Services.AddHttpClient<IUnidadOrganizativaApiClient, UnidadOrganizativaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
