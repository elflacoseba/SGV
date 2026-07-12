using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Common;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Habilidad;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Compatibilidad histórica para PR 2b-3 (cross-módulo
/// <c>HabilidadesCargosModelTests</c>): conserva las firmas
/// <c>Task&lt;HttpClient&gt;</c> y la base factory, pero delega el grueso del
/// trabajo al composite <see cref="WebIntegrationFixture"/> para evitar
/// repetir la cadena <c>WithOverrides</c> + sign-in. El host resultante es
/// el mismo que produce <see cref="WebIntegrationFixture.CreateCargoLeaseAsync"/>;
/// sólo cambia el envoltorio: aquí devolvemos <see cref="HttpClient"/> para
/// no romper los call sites que aún no migraron a <see cref="WebClientLease"/>.
/// </summary>
public sealed class CargoWebTestFixture : IDisposable
{
    private readonly WebIntegrationFixture _root;

    public CargoWebTestFixture() => _root = new WebIntegrationFixture();

    /// <summary>
    /// Devuelve la raíz del composite para casos que necesiten construir un
    /// cliente anónimo o crear su propio lease (futuro PR 2b-3).
    /// </summary>
    public SgvWebApplicationFactory BaseFactory => _root.RootFactory;

    /// <summary>
    /// Devuelve un factory con <see cref="ICargoApiClient"/> sobrescrito
    /// para el fake provisto. Encadena sobre la raíz del composite para no
    /// crear hosts adicionales nunca dispuestos.
    /// </summary>
    public SgvWebApplicationFactory WithCargoApiClient(FakeCargoApiClient fake)
        => _root.RootFactory.WithOverrides(cargoApiClient: fake);

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid JuniorNivelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid SeniorNivelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Builds a fresh <see cref="CargoDto"/> with random id and nivel id,
    /// useful when a test only cares about the data shape.
    /// </summary>
    public static CargoDto BuildCargoDto(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    /// <summary>
    /// Returns an authenticated <see cref="HttpClient"/> whose
    /// <see cref="ICargoApiClient"/> resolves to <paramref name="apiClient"/>.
    /// Thin shim sobre <see cref="WebIntegrationFixture.CreateCargoLeaseAsync"/>
    /// que retorna <see cref="WebClientLease.Client"/> mientras la lease queda
    /// retenida en el fixture hasta su <see cref="Dispose"/>.
    /// </summary>
    public Task<HttpClient> CreateAuthenticatedClientAsync(FakeCargoApiClient apiClient)
        => CreateAuthenticatedClientAsync(apiClient, new FakeHabilidadApiClient(), adminRole: false);

    public Task<HttpClient> CreateAdminClientAsync(FakeCargoApiClient apiClient)
        => CreateAuthenticatedClientAsync(apiClient, new FakeHabilidadApiClient(), adminRole: true);

    /// <summary>
    /// Variante sobrecargada que también inyecta un
    /// <see cref="FakeHabilidadApiClient"/> en el contenedor y permite
    /// optar por autenticar con rol <see cref="RolesSgv.Administrador"/>.
    /// Internamente solicita un <see cref="WebClientLease"/> al composite y
    /// entrega el <see cref="WebClientLease.Client"/> al test; el fixture
    /// conserva la lease hasta <see cref="Dispose"/> para que la factory
    /// no quede huérfana.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        FakeCargoApiClient apiClient,
        FakeHabilidadApiClient habilidadApiClient,
        bool adminRole)
    {
        var lease = await _root.CreateCargoLeaseAsync(apiClient, habilidadApiClient, adminRole);
        _leases.Add(lease);
        return lease.Client;
    }

    /// <summary>
    /// Extracts the antiforgery token rendered in a <c>__RequestVerificationToken</c>
    /// hidden input. Fails the test if the token is not present.
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

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}