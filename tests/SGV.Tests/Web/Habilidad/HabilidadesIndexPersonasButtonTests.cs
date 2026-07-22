using System.Net;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del botón "Personas" introducido en el change
/// <c>agrega-navegacion-personas-habilidades</c> (PR C — frontend
/// subreverso, task C.5 / C.6). Cobertura de:
///   - REQ-HLD-NEW (botón Personas por habilidad activa).
///   - REQ-HLD-NEW-VISIBILITY (visible solo cuando !Model.IsDeletedView).
///   - REQ-HLD-NEW-POSITION (entre Cargos y Editar).
///   - Helper <c>BuildPersonasRouteValues</c> preserva contexto (espejo
///     de <c>BuildCargosRouteValues</c>).
/// </summary>
[Collection("WebIntegration")]
public sealed class HabilidadesIndexPersonasButtonTests
{
    private readonly WebIntegrationFixture _fixture;

    public HabilidadesIndexPersonasButtonTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_ActiveRow_RendersPersonasButtonWithPreservedContext()
    {
        // REQ-HLD-NEW + REQ-HLD-NEW-VISIBILITY: la fila activa MUST exponer
        // el botón Personas hacia /organizacion/habilidades/{id}/personas.
        var habilidad = new HabilidadDto(Guid.NewGuid(), "HAB-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync(
            "/organizacion/habilidades?p=1&search=lid&sort=nombre_desc&status=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El <a> debe tener aria-label específico y href al subrecurso personas.
        Assert.Contains(
            $"aria-label=\"Personas de {habilidad.Nombre}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/habilidades/{habilidad.Id}/personas",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El href debe preservar p/search/sort del listado de origen.
        // Nota: en vista activas Segmento == null, status no viaja en la URL.
        Assert.Contains("p=1", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=lid", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", content, StringComparison.OrdinalIgnoreCase);

        // El ícono debe ser el icono "users" del design (ti-users).
        Assert.Contains("ti ti-users", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_DeletedRow_DoesNotRenderPersonasButton()
    {
        // REQ-HLD-NEW-VISIBILITY: la fila eliminada MUST NOT exponer el
        // botón Personas (sólo Reactivar).
        var habilidadEliminada = new HabilidadDto(Guid.NewGuid(), "HAB-DEL", "Habilidad Eliminada", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.QueryHandler = query =>
            string.Equals(query?.Status, "eliminadas", StringComparison.OrdinalIgnoreCase)
                ? new PagedResult<HabilidadDto>([habilidadEliminada], 1, 1, 20)
                : new PagedResult<HabilidadDto>([], 0, query!.Page, query.PageSize);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/habilidades?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de habilidades eliminadas", content, StringComparison.OrdinalIgnoreCase);

        // El CTA "Personas" no debe aparecer en ninguna fila del segmento
        // eliminado. Verificamos la presencia/ausencia del <a> específico con
        // su aria-label y href, no el icono aislado (porque "users" puede
        // aparecer en otros lugares).
        Assert.DoesNotContain(
            $"aria-label=\"Personas de {habilidadEliminada.Nombre}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"href=\"/organizacion/habilidades/{habilidadEliminada.Id}/personas\"",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_ActiveRow_PersonasButton_AppearsBetweenCargosAndEditar()
    {
        // REQ-HLD-NEW-POSITION: el botón Personas MUST ubicarse en la
        // columna Acciones, entre Cargos y Editar.
        var habilidad = new HabilidadDto(Guid.NewGuid(), "HAB-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/habilidades?status=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El orden textual esperado en la columna Acciones de la fila activa:
        // Detalle → Cargos → Personas → Editar → Eliminar.
        var cargosIdx = content.IndexOf(
            $"aria-label=\"Cargos de {habilidad.Nombre}\"",
            StringComparison.OrdinalIgnoreCase);
        var personasIdx = content.IndexOf(
            $"aria-label=\"Personas de {habilidad.Nombre}\"",
            StringComparison.OrdinalIgnoreCase);
        var editarIdx = content.IndexOf(
            $"aria-label=\"Editar {habilidad.Nombre}\"",
            StringComparison.OrdinalIgnoreCase);

        Assert.True(cargosIdx > 0, "El botón Cargos debe estar presente.");
        Assert.True(personasIdx > 0, "El botón Personas debe estar presente.");
        Assert.True(editarIdx > 0, "El botón Editar debe estar presente.");
        Assert.True(cargosIdx < personasIdx,
            $"Personas debe aparecer después de Cargos (cargos={cargosIdx}, personas={personasIdx}).");
        Assert.True(personasIdx < editarIdx,
            $"Personas debe aparecer antes de Editar (personas={personasIdx}, editar={editarIdx}).");
    }
}