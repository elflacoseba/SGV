using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

public sealed class SignInModel(IAuthApiClient authApiClient) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new LoginRequest(Input.UserNameOrEmail, Input.Password);
        var response = await authApiClient.LoginAsync(request, cancellationToken);

        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            return Page();
        }

        var principal = AuthSessionFactory.CreatePrincipal(request, response);
        var properties = AuthSessionFactory.CreateProperties(response);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return LocalRedirect("/");
    }

    public sealed class InputModel
    {
        [Required]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
