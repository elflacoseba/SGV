using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Web smoke tests para la página Details del módulo Puestos (PR 3C).
/// Espejo de <c>CargoDetailsPageTests</c> ajustado al contrato de Puestos:
/// <list type="bullet">
///   <item>El render es readonly con <c>dl.row</c> estilo Inspinia.</item>
///   <item>Los campos son Codigo, Nombre, Descripcion?, UnidadOrganizativaId (como nombre), CargoId (como nombre) y PuestoSuperiorId (como link).</item>
///   <item>El link <c>Editar</c> preserva <c>search</c>/<c>sort</c>/<c>status</c> para volver al Index desde Edit con el contexto del listado.</item>
///   <item>El link <c>Volver al listado</c> preserva el mismo contexto.</item>
///   <item>Cuando el puesto no existe (<c>GetByIdAsync</c> devuelve <c>null</c>), la página muestra estado recuperable.</item>
/// </list>
/// Usa <see cref="SgvWebApplicationFactory"/> + <see cref="FakePuestosApiClient"/>
/// para no requerir MySQL.
/// </summary>
public sealed class PuestoDetailsPageTests : IClassFixture<PuestoWebTestFixture>
{
    private readonly PuestoWebTestFixture _fixture;

    public PuestoDetailsPageTests(PuestoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Spec 3C.1 · Req 1 — Acceso anónimo redirige a /auth/sign-in
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        // Cliente sin autenticación: usa la base factory sin overrides para
        // que [Authorize] de la página dispare el challenge.
        var client = _fixture.BaseFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/puestos/detalles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/auth/sign-in", location, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3C.1 · Req 5 — Render readonly con dl.row mostrando todos los campos
    //
    // El HTML renderizado por Details MUST contener un <dl class="row mb-0">
    // con los siguientes pares <dt>/<dd>: Código, Nombre, Descripción,
    // Unidad organizativa (renderiza el nombre), Cargo (renderiza el nombre)
    // y Puesto superior (sin superior: "Sin superior"; con superior: link).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticated_ShowsPuestoReadOnly()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-DET-001",
            "Detalle Puesto",
            "Detalle del puesto de pruebas",
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            PuestoSuperiorId: null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos/detalles/{puestoId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Debe existir el bloque <dl class="row mb-0"> estilo Inspinia.
        Assert.Matches(
            new Regex(@"<dl[^>]*class=""[^""]*\brow\b[^""]*mb-0[^""]*""", RegexOptions.IgnoreCase),
            content);

        // Campos visibles: Codigo, Nombre, Descripcion (renderiza el texto).
        Assert.Contains("P-DET-001", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Detalle Puesto", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Detalle del puesto de pruebas", content, StringComparison.OrdinalIgnoreCase);

        // UnidadOrganizativaId y CargoId deben renderizarse como NOMBRE (no Guid).
        Assert.Contains("Comercial", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vendedor", content, StringComparison.OrdinalIgnoreCase);

        // Sin superior: "Sin superior" (texto literal).
        Assert.Contains("Sin superior", content, StringComparison.OrdinalIgnoreCase);

        // El endpoint /api/v1/puestos/{id} debe haber sido consultado una vez.
        var byIdCall = Assert.Single(apiClient.GetByIdCalls);
        Assert.Equal(puestoId, byIdCall);

        // Acción "Volver al listado" presente.
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/organizacion/puestos", content, StringComparison.OrdinalIgnoreCase);

        // Acción "Editar" presente con href a la página Edit del mismo id.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/puestos/editar/{puestoId}",
            content,
            StringComparison.OrdinalIgnoreCase);

        // Spec 3C.1 · tokens prohibidos en Details.
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reactivar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3C.1 · Req 5 — Estado recuperable cuando el puesto no existe.
    //
    // Cuando GetByIdAsync devuelve null (404 del API o el id fue eliminado
    // lógicamente) la página MUST mostrar un estado recuperable con un link
    // "Volver al listado" que preserve el contexto.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPuestoNotFound_ShowsNotAvailableState()
    {
        // Fake sin GetByIdResult → GetByIdAsync devuelve null.
        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = null,
            GetAllResult = Array.Empty<PuestoDto>()
        };
        var missingId = Guid.NewGuid();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos/detalles/{missingId}?p=2&search=foo&sort=codigo_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Mensaje de no disponible.
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // En estado recuperable, NO debe renderizar el dl.row con campos del puesto.
        Assert.DoesNotContain("<dl", content, StringComparison.OrdinalIgnoreCase);

        // "Volver al listado" sigue presente preservando el contexto de retorno.
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);

        // El endpoint /api/v1/puestos/{missingId} fue consultado una vez (y devolvió null).
        var byIdCall = Assert.Single(apiClient.GetByIdCalls);
        Assert.Equal(missingId, byIdCall);

        // Tokens prohibidos siguen ausentes.
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Editar<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reactivar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3C.1 · Req 5 — Retorno al listado preservando contexto.
    //
    // El link "Volver al listado" debe preservar search, sort y status del
    // query string de entrada. El comando del orquestador indica que el link
    // debe pasar p/search/sort (status se mapea vía returnStatus en Index).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticated_BackLinkPreservesContext()
    {
        var puestoId = Guid.NewGuid();
        var puesto = new PuestoDto(
            puestoId,
            "P-BACK-001",
            "Back Link Puesto",
            null,
            PuestoWebTestFixture.SampleUnidadOrganizativaId,
            "Comercial",
            PuestoWebTestFixture.SampleCargoId,
            "Vendedor",
            PuestoSuperiorId: null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        // Entramos con p=3, search=back, sort=codigo_desc, returnStatus=eliminadas
        // (forward-compat con puestos-filtro-activos-eliminados). Usamos
        // returnStatus porque es el nombre del parámetro que espera
        // DetailsModel.OnGetAsync (status se usa en Index como filtro activo,
        // no en Details). El link de retorno al Index mapea returnStatus a
        // status automáticamente (BuildIndexRouteValuesForReturn).
        var response = await client.GetAsync(
            $"/organizacion/puestos/detalles/{puestoId}?p=3&search=back&sort=codigo_desc&returnStatus=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El href de "Volver al listado" debe ir a Index preservando contexto,
        // con el segmento mapeado a status (lo que Index espera).
        Assert.Contains(
            "href=\"/organizacion/puestos?",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=back", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=codigo_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3C.1 · Req 5 — Puesto superior renderiza link preservando contexto.
    //
    // Cuando el DTO tiene PuestoSuperiorId != null, el campo "Puesto superior"
    // MUST renderizar un <a> al detalle del superior preservando
    // search/sort/status del query string de entrada (paridad con el patrón
    // del Index).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPuestoHasSuperior_RendersLinkToSuperior()
    {
        var superiorId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var superior = new PuestoDto(
            superiorId,
            "P-SUP",
            "Puesto Superior",
            null,
            unidadId,
            "Gerencia",
            cargoId,
            "Gerente",
            PuestoSuperiorId: null);
        var child = new PuestoDto(
            puestoId,
            "P-CHILD",
            "Puesto Dependiente",
            null,
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            PuestoSuperiorId: superiorId);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = child,
            GetAllResult = new[] { superior, child }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        // Entramos con search/sort/returnStatus para verificar que se preservan en el link al superior.
        // Usamos returnStatus (no status) porque es el nombre del parámetro que espera
        // DetailsModel.OnGetAsync. El link al superior usa returnStatus para preservar
        // el contexto del segmento (BuildSuperiorUrl).
        var response = await client.GetAsync(
            $"/organizacion/puestos/detalles/{puestoId}?p=1&search=dep&sort=nombre_asc&returnStatus=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El HTML debe contener un link al detalle del superior con el id del Guid.
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{superiorId}",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El contexto search/sort/returnStatus debe preservarse en el link.
        Assert.Contains("search=dep", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_asc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnStatus=eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }
}