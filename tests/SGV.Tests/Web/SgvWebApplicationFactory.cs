using Microsoft.AspNetCore.Mvc.Testing;

namespace SGV.Tests.Web;

/// <summary>
/// WebApplicationFactory for SGV.Web (Razor Pages shell).
/// No additional service configuration needed — the shell is self-contained.
/// </summary>
public sealed class SgvWebApplicationFactory : WebApplicationFactory<SGV.Web.Program>
{
}
