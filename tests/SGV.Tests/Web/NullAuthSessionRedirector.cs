using Microsoft.AspNetCore.Mvc;
using SGV.Web.Integration.Common;

namespace SGV.Tests.Web;

/// <summary>
/// Fake de <see cref="IAuthSessionRedirector"/> para tests que instancian
/// PageModels directamente sin el harness web completo. Retorna
/// <see langword="null"/> siempre (no hay HttpContext en el contexto del
/// test) — los tests que sí verifican el comportamiento del redirect
/// usan el real con un <see cref="Microsoft.AspNetCore.Http.DefaultHttpContext"/>.
/// </summary>
internal sealed class NullAuthSessionRedirector : IAuthSessionRedirector
{
    public IActionResult? TryRedirectToLogin(string? returnUrl = null) => null;
}