using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed partial class CargoHabilidadesPageTests
{
    // ──────────────────────────────────────────────
    // T3.5 — Acceso restringido (Req 1)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Anonymous_RedirectsToSignIn()
    {
        // Anónimo: usamos factory local porque el lease anónimo del composite
        // dispose la _root al terminar, rompiendo tests hermanos. (Bug
        // documentado en apply-progress de PR 2b-1.)
        using var localFactory = new SgvWebApplicationFactory();
        using var client = localFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/cargos/{Guid.NewGuid()}/habilidades");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        // El factory fixture existente produce un principal SIN role-claims,
        // por lo que
        // User.IsInRole(RolesSgv.Administrador) devuelve false y la página
        // emite Forbid(). El cookie auth scheme configurado en Program.cs
        // tiene AccessDeniedPath="/error/403", así que Forbid() se traduce
        // a un 302 redirect hacia esa ruta — equivalente observable para
        // el navegador y consistente con el patrón del repo (Forbid en
        // lugar de 403 plano cuando hay sesión autenticada).
        var apiClient = FakeCargoApiClient.WithCargoList();
        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/organizacion/cargos/{Guid.NewGuid()}/habilidades");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}
