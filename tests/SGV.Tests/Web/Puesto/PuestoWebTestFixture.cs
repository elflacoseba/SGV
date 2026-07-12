using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Organizacion;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Shim sobre <see cref="WebIntegrationFixture"/> que conserva las cuatro
/// firmas <c>Task&lt;WebClientLease&gt;</c> originales
/// (<c>CreateAuthenticatedClientAsync(FakePuestosApiClient)</c>,
/// <c>CreateAdminClientAsync(FakePuestosApiClient)</c>,
/// <c>CreateAdminClientAsync(unidad, cargo, puestos)</c> y
/// <c>CreateAuthenticatedClientAsync(unidad, cargo, puestos, bool)</c>)
/// delegando a <see cref="WebIntegrationFixture.CreatePuestoLeaseAsync"/>.
/// Las cuatro clases PageTests + PuestoWebSeamTests consumen
/// <see cref="WebIntegrationFixture"/> directamente vía
/// <c>[Collection("WebIntegration")]</c>; este fixture existe únicamente para
/// <see cref="PuestoWebTestFixtureLeaseContractTests"/>, que valida que la
/// delegación al composite sigue intacta.
/// </summary>
public sealed class PuestoWebTestFixture : IAsyncDisposable
{
    private readonly WebIntegrationFixture _root;

    public PuestoWebTestFixture() => _root = new WebIntegrationFixture();

    /// <summary>
    /// Acceso a la raíz del composite. Sólo para los contract tests, que
    /// necesitan comparar el <see cref="WebClientLease.Factory"/> del lease
    /// contra la raíz compartida del fixture y verificar el aislamiento del
    /// dispose. Las páginas no deben usar este accessor: consumen el lease
    /// directamente vía <see cref="WebIntegrationFixture"/>.
    /// </summary>
    public SgvWebApplicationFactory RootFactory => _root.RootFactory;

    /// <summary>Lease autenticado (no admin) contra el módulo de Puestos.</summary>
    public Task<WebClientLease> CreateAuthenticatedClientAsync(FakePuestosApiClient apiClient)
        => _root.CreatePuestoLeaseAsync(apiClient);

    /// <summary>Lease autenticado con rol Administrador.</summary>
    public Task<WebClientLease> CreateAdminClientAsync(FakePuestosApiClient apiClient)
        => _root.CreatePuestoLeaseAsync(apiClient, adminRole: true);

    /// <summary>Lease admin con los tres fakes de catálogo inyectados.</summary>
    public Task<WebClientLease> CreateAdminClientAsync(
        IUnidadOrganizativaApiClient unidadFake,
        ICargoApiClient cargoFake,
        FakePuestosApiClient puestosFake)
        => _root.CreatePuestoLeaseAsync(puestosFake, unidadFake, cargoFake, adminRole: true);

    /// <summary>
    /// Variante sobrecargada con los tres overrides activos. Conservada como
    /// firma externa; delega al composite.
    /// </summary>
    public Task<WebClientLease> CreateAuthenticatedClientAsync(
        IUnidadOrganizativaApiClient unidadFake,
        ICargoApiClient cargoFake,
        FakePuestosApiClient puestosFake,
        bool adminRole)
        => _root.CreatePuestoLeaseAsync(puestosFake, unidadFake, cargoFake, adminRole);

    public async ValueTask DisposeAsync() => await _root.DisposeAsync();
}
