using SGV.Tests.Web.Collections;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Shim sobre <see cref="WebIntegrationFixture"/> que conserva la firma
/// <c>Task&lt;WebClientLease&gt;</c> original
/// (<c>CreateAuthenticatedClientAsync(FakeHabilidadApiClient)</c>) delegando a
/// <see cref="WebIntegrationFixture.CreateHabilidadLeaseAsync"/>. Las cinco
/// clases PageTests consumen <see cref="WebIntegrationFixture"/> directamente
/// vía <c>[Collection("WebIntegration")]</c>; este fixture existe únicamente
/// para <see cref="HabilidadWebTestFixtureLeaseContractTests"/>, que valida que
/// la delegación al composite sigue intacta. Los helpers de estado (builders,
/// markup, handler de auth y extractor de antiforgery) viven ahora en
/// <see cref="WebTestBuilders"/> y <see cref="HabilidadMarkup"/>.
/// </summary>
public sealed class HabilidadWebTestFixture : IAsyncDisposable
{
    private readonly WebIntegrationFixture _root;

    public HabilidadWebTestFixture() => _root = new WebIntegrationFixture();

    /// <summary>
    /// Acceso a la raíz del composite. Sólo para los contract tests, que
    /// necesitan comparar el <see cref="WebClientLease.Factory"/> del lease
    /// contra la raíz compartida del fixture y verificar el aislamiento del
    /// dispose. Las páginas no deben usar este accessor: consumen el lease
    /// directamente vía <see cref="WebIntegrationFixture"/>.
    /// </summary>
    public SgvWebApplicationFactory RootFactory => _root.RootFactory;

    /// <summary>Lease autenticado (no admin) contra el módulo de Habilidades.</summary>
    public Task<WebClientLease> CreateAuthenticatedClientAsync(FakeHabilidadApiClient apiClient)
        => _root.CreateHabilidadLeaseAsync(apiClient);

    public async ValueTask DisposeAsync() => await _root.DisposeAsync();
}
