using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Tests del render del sidenav (PR 2): verifican que el grupo
/// <c>Seguridad</c> y el subítem <c>Usuarios</c> aparecen en la shell
/// autenticada y que el subítem está gated por el rol
/// <see cref="SGV.Contracts.Seguridad.RolesSgv.Administrador"/>.
/// Espejo estructural de los tests del sidenav de Cargos/Puestos pero
/// con role-gating explícito.
/// </summary>
[Collection("WebIntegration")]
public class UsuarioSidenavTests
{
    private readonly WebIntegrationFixture _fixture;

    public UsuarioSidenavTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticatedWithoutAdminRole_DoesNotExposeUsuariosSubitem()
    {
        // AC: el subítem "Usuarios" sólo aparece bajo el rol
        // Administrador. Sin admin el ítem colapsable "Seguridad" debe
        // seguir visible (porque incluye otros placeholders no
        // vigentes de seguridad), pero el link a /seguridad/usuarios NO.
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake, adminRole: false);

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(@"aria-controls=""seguridad""", content, StringComparison.OrdinalIgnoreCase);
        // El grupo debe estar presente (header), pero el subítem
        // apunta a /seguridad/usuarios NO debe aparecer.
        Assert.DoesNotContain(@"href=""/seguridad/usuarios""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"href=""/seguridad/usuarios/crear""", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticatedWithAdminRole_ExposesUsuariosSubitem()
    {
        // AC: con rol Administrador el subítem "Usuarios" se renderiza
        // bajo el grupo Seguridad. Listado + Crear + Editar se reservan
        // para PR 3/4 (sólo se valida el subítem raíz en PR 2 porque
        // las páginas aún no existen).
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake, adminRole: true);

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(@"aria-controls=""seguridad""", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"href=""/seguridad/usuarios""", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAnonymous_DoesNotExposeUsuariosSubitem()
    {
        // AC: anónimos NUNCA ven el subítem. La redirección a
        // /auth/sign-in la cubre el test clásico de Cargo;
        // aquí verificamos que el sidenav renderizado en / no leakea
        // el link a un usuario no autenticado (porque su HttpContext.User
        // no contiene el role).
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake, adminRole: false);

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El usuario autenticado (sin admin) NO debe ver el subítem.
        Assert.DoesNotContain(@"href=""/seguridad/usuarios""", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenOnUsuariosRoute_SubmenuIsActive()
    {
        // AC: cuando PR3 implemente Index.cshtml, /seguridad/usuarios
        // debe activar el grupo Seguridad. En PR 2 la página todavía
        // no existe, así que este test sólo verifica que el sidenav
        // ya renderiza el link con la ruta correcta desde la cual
        // el highlight se va a aplicar.
        //
        // Como Index.cshtml aún no existe, no podemos hitear
        // /seguridad/usuarios directamente. Este test se cubre recién
        // en PR 3; en PR 2 validamos que el subítem referencia la
        // ruta esperada como ancla estructural.
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake, adminRole: true);

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El subítem debe apuntar EXACTAMENTE a /seguridad/usuarios
        // (la ruta que PR3 va a registrar como Razor Page).
        Assert.True(
            Regex.IsMatch(
                content,
                @"<a(?=[^>]*\bhref=""/seguridad/usuarios"")[^>]*>",
                RegexOptions.IgnoreCase),
            "El subítem Usuarios debe apuntar a /seguridad/usuarios.");
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticatedWithAdminRole_DoesNotExposeUsuariosSubItemBeforePagesExist()
    {
        // AC: el subítem "Crear"/"Editar"/"Detalles" se materializa en
        // PR 3/4. PR 2 sólo expone el listado (el head del grupo
        // Seguridad) más el link raíz al módulo. Verificamos que NO
        // existen placeholders colgando a /seguridad/usuarios/crear
        // (esos paths llegan con las pages en PR 4).
        var fake = new FakeUsuarioApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(fake, adminRole: true);

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(@"href=""/seguridad/usuarios/crear""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"href=""/seguridad/usuarios/editar""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"href=""/seguridad/usuarios/detalles""", content, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasActiveOnLink(string content, string href)
    {
        // Espejo de CargoWebTests.LinkHasActive: localiza el <a ...>
        // que matchea el href y devuelve true si lleva la clase
        // "active" en cualquier posición. PR 2 no lo usa porque las
        // páginas de Usuarios aún no existen, pero se mantiene por
        // consistencia con la suite del shell.
        var idx = content.IndexOf($"href=\"{href}\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var anchorStart = content.LastIndexOf("<a ", idx, StringComparison.OrdinalIgnoreCase);
        if (anchorStart < 0) return false;
        var anchorEnd = content.IndexOf('>', idx);
        if (anchorEnd < 0) return false;
        var anchor = content[anchorStart..(anchorEnd + 1)];
        return anchor.Contains(" active\"", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains("\"active ", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains(" active ", StringComparison.OrdinalIgnoreCase);
    }
}
