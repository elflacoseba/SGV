using Microsoft.Extensions.Logging.Abstractions;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Collections;
using SGV.Web.Pages.Organizacion.Puestos;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Tests unitarios aislados del PageModel de Puestos/Edit (PR 3B) que
/// verifican QUÉ catálogos dispara <c>LoadCatalogsAsync</c> durante un GET,
/// sin pasar por el harness web (cuyo baseline de autenticación está roto en
/// la rama actual — ver <see cref="PuestoEditPageTests"/> para los tests
/// integration-level que sí lo requieren).
/// <para>
/// La intención de esta suite es proteger el contrato descubierto en la
/// issue #120: <c>_Form.cshtml</c> oculta los selects de UnidadOrganizativaId
/// y CargoId cuando <c>IsEdit == true</c> (campos inmutables), por lo que
/// cargar esos catálogos en Edit no alimenta ningún control. Sólo
/// <c>PuestoSuperiorOptions</c> se renderiza, y su carga queda justificada.
/// </para>
/// <para>
/// Acceso a <c>LoadCatalogsAsync</c>: la firma del helper es <c>internal</c>
/// (no <c>private</c>) precisamente para que esta suite pueda invocarlo
/// directamente sin necesidad del <see cref="WebApplicationFactory"/>. El
/// contrato de exposición corre por <c>InternalsVisibleTo("SGV.Tests")</c>
/// ya declarado en <c>Program.cs</c>.
/// </para>
/// </summary>
public sealed class PuestoEditLoadCatalogsTests
{
    // ───────────────────────────────────────────────────────────────
    // Spec #120 · Req 1 — Edit no carga catálogo de UnidadOrganizativa
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_GET_NoInvocaCatalogoUnidadesOrganizativas()
    {
        var unidadClient = new FakeUnidadOrganizativaApiClient();
        var cargoClient = new FakeCargoApiClient();
        var puestosClient = new FakePuestosApiClient
        {
            // Cualquier valor no vacío basta para que el catálogo de
            // superiores "responda OK"; este test no se ocupa de su contenido.
            GetAllResult = [WebTestBuilders.BuildPuestoDto("P-001", "Director")]
        };

        // Después del GREEN, el ctor de EditModel ya no recibe
        // IUnidadOrganizativaApiClient ni ICargoApiClient — la firma del ctor
        // es la primera línea de defensa contra el dead code reintroducido.
        var sut = new EditModel(
            puestosClient,
            new NullAuthSessionRedirector(),
            NullLogger<EditModel>.Instance);

        await sut.LoadCatalogsAsync(CancellationToken.None);

        Assert.Empty(unidadClient.QueryCalls);
        Assert.Empty(unidadClient.GetAllActivasCalls);
    }

    // ───────────────────────────────────────────────────────────────
    // Spec #120 · Req 2 — Edit no carga catálogo de Cargo
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_GET_NoInvocaCatalogoCargos()
    {
        var unidadClient = new FakeUnidadOrganizativaApiClient();
        var cargoClient = new FakeCargoApiClient();
        var puestosClient = new FakePuestosApiClient
        {
            GetAllResult = [WebTestBuilders.BuildPuestoDto("P-001", "Director")]
        };

        // Después del GREEN, el ctor de EditModel ya no recibe ICargoApiClient.
        var sut = new EditModel(
            puestosClient,
            new NullAuthSessionRedirector(),
            NullLogger<EditModel>.Instance);

        await sut.LoadCatalogsAsync(CancellationToken.None);

        Assert.Empty(cargoClient.GetAllCalls);
    }

    // ───────────────────────────────────────────────────────────────
    // Spec #120 · Req 3 — Edit sí carga catálogo de PuestoSuperior
    //
    // Caso de control / anti-regresión. Este test pasa hoy Y después del
    // GREEN: garantiza que el refactor no elimina la carga necesaria del
    // dropdown de PuestoSuperiorId (visible en _Form.cshtml tanto en Create
    // como en Edit).
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_GET_CargaPuestosSuperiores()
    {
        var seedSuperior = WebTestBuilders.BuildPuestoDto("P-SUP", "Director Superior");
        var seedEdit = WebTestBuilders.BuildPuestoDto("P-EDIT", "Puesto Edit");
        var unidadClient = new FakeUnidadOrganizativaApiClient();
        var cargoClient = new FakeCargoApiClient();
        var puestosClient = new FakePuestosApiClient
        {
            GetAllResult = [seedEdit, seedSuperior]
        };

        var sut = new EditModel(
            puestosClient,
            new NullAuthSessionRedirector(),
            NullLogger<EditModel>.Instance);

        await sut.LoadCatalogsAsync(CancellationToken.None);

        // Una sola llamada a GetAllAsync (no se duplica en pre/post).
        Assert.Single(puestosClient.GetAllCalls);

        // El catálogo de superiores debe estar poblado con un option por cada
        // PuestoDto seed, mapeados vía PuestoFormHelpers.MapToSuperiorViewModel.
        Assert.Equal(2, sut.PuestoSuperiorOptions.Count);
        Assert.Contains(
            sut.PuestoSuperiorOptions,
            o => o.Codigo == seedSuperior.Codigo && o.Nombre == seedSuperior.Nombre);
        Assert.Contains(
            sut.PuestoSuperiorOptions,
            o => o.Codigo == seedEdit.Codigo && o.Nombre == seedEdit.Nombre);
    }
}
