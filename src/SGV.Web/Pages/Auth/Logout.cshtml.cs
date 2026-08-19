using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SGV.Web.Pages.Auth;

/// <summary>
/// POST-only logout. <see cref="AutoValidateAntiforgeryTokenAttribute"/>
/// cierra el vector C-2 (CSRF contra sign-out): un atacante no puede
/// desautenticar al usuario forzando un POST a <c>/auth/logout</c> desde
/// un sitio externo porque el token antiforgery no viaja en cross-site
/// POST con cookies <c>SameSite=Lax</c>.
/// </summary>
[AutoValidateAntiforgeryToken]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/auth/sign-in");
    }
}
