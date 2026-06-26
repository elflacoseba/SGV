using Microsoft.AspNetCore.Authentication.Cookies;
using SGV.Web.Integration.Auth;

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

builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
});

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
