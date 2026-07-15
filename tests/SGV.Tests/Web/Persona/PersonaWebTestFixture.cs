using System.Text.RegularExpressions;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Shim sobre <see cref="WebIntegrationFixture"/> que conserva la firma
/// <c>Task&lt;HttpClient&gt;</c> histórica para compatibilidad con PRs
/// previos, delegando al composite <see cref="WebIntegrationFixture.CreatePersonaLeaseAsync"/>
/// para evitar repetir la cadena <c>WithOverrides</c> + sign-in. El host
/// resultante es el mismo que produce el composite; sólo cambia el
/// envoltorio: aquí devolvemos <see cref="HttpClient"/> para no romper
/// call sites que aún no migraron a <see cref="WebClientLease"/>.
/// </summary>
public sealed class PersonaWebTestFixture : IAsyncDisposable
{
    private readonly WebIntegrationFixture _root;

    public PersonaWebTestFixture() => _root = new WebIntegrationFixture();

    /// <summary>Acceso a la raíz del composite. Sólo para los contract tests.</summary>
    public SgvWebApplicationFactory BaseFactory => _root.RootFactory;

    /// <summary>
    /// Devuelve un factory con <see cref="IPersonaApiClient"/> sobrescrito
    /// para el fake provisto. Encadena sobre la raíz del composite.
    /// </summary>
    public SgvWebApplicationFactory WithPersonaApiClient(IPersonaApiClient fake)
        => _root.RootFactory.WithOverrides(personaApiClient: fake);

    /// <summary>
    /// Devuelve un <see cref="HttpClient"/> autenticado contra el host del
    /// composite, con <see cref="IPersonaApiClient"/> cableado al fake
    /// provisto. Conserva la firma histórica de los tests web para
    /// minimizar cambios en el call sites.
    /// </summary>
    public Task<HttpClient> CreateAuthenticatedClientAsync(IPersonaApiClient apiClient)
        => CreateAuthenticatedClientAsync(apiClient, adminRole: false);

    /// <summary>Variante con rol <c>Administrador</c>.</summary>
    public Task<HttpClient> CreateAdminClientAsync(IPersonaApiClient apiClient)
        => CreateAuthenticatedClientAsync(apiClient, adminRole: true);

    /// <summary>
    /// Variante sobrecargada que permite elegir el rol. Internamente
    /// solicita un <see cref="WebClientLease"/> al composite y entrega el
    /// <see cref="WebClientLease.Client"/> al test; el fixture conserva la
    /// lease hasta <see cref="DisposeAsync"/> para que la factory no quede
    /// huérfana.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(IPersonaApiClient apiClient, bool adminRole)
    {
        var lease = await _root.CreatePersonaLeaseAsync(apiClient, adminRole);
        _leases.Add(lease);
        return lease.Client;
    }

    /// <summary>
    /// Extrae el token antiforgery renderizado en el hidden input
    /// <c>__RequestVerificationToken</c>. Falla el test si el token no
    /// está presente.
    /// </summary>
    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    private readonly List<WebClientLease> _leases = new();

    public async ValueTask DisposeAsync()
    {
        foreach (var lease in _leases)
        {
            await lease.DisposeAsync();
        }
        _leases.Clear();
        await _root.DisposeAsync();
    }
}