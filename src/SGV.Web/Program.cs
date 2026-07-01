using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using SGV.Web.Integration.Auth;
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
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
