using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Tests.Web.Habilidad;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Tests RED (strict TDD) para la migración del módulo Puesto al composite
/// <see cref="WebIntegrationFixture"/>. Cubren el contrato de las cuatro firmas
/// heredadas de <c>PuestoWebTestFixture</c> (líneas 89/96/103/120 originales)
/// ahora delegadas a <see cref="WebIntegrationFixture.CreatePuestoLeaseAsync"/>.
/// Si el fixture deja de delegar al composite, estos tests rompen antes que
/// los call sites de páginas, exponiendo drift durante el refactor.
/// </summary>
public sealed class PuestoWebTestFixtureLeaseContractTests
{
    [Fact]
    public async Task CreateAuthenticatedClientAsync_ReturnsLeaseNotHttpClient()
    {
        await using var fixture = new PuestoWebTestFixture();

        var lease = await fixture.CreateAuthenticatedClientAsync(new FakePuestosApiClient());

        Assert.NotNull(lease);
        Assert.NotNull(lease.Client);
    }

    [Fact]
    public async Task CreateAdminClientAsync_WithFakeOnly_ReturnsLease()
    {
        await using var fixture = new PuestoWebTestFixture();

        var lease = await fixture.CreateAdminClientAsync(new FakePuestosApiClient());

        Assert.NotNull(lease);
        Assert.NotNull(lease.Client);
    }

    [Fact]
    public async Task CreateAdminClientAsync_WithThreeFakes_ReturnsLease()
    {
        await using var fixture = new PuestoWebTestFixture();

        var lease = await fixture.CreateAdminClientAsync(
            new FakeUnidadOrganizativaApiClient(),
            new FakeCargoApiClient(),
            new FakePuestosApiClient());

        Assert.NotNull(lease);
        Assert.NotNull(lease.Client);
    }

    [Fact]
    public async Task CreateAuthenticatedClientAsync_FourArgOverload_ReturnsLease()
    {
        await using var fixture = new PuestoWebTestFixture();

        var lease = await fixture.CreateAuthenticatedClientAsync(
            new FakeUnidadOrganizativaApiClient(),
            new FakeCargoApiClient(),
            new FakePuestosApiClient(),
            adminRole: false);

        Assert.NotNull(lease);
        Assert.NotNull(lease.Client);
    }

    [Fact]
    public async Task Lease_DelegatedToComposite_HasDistinctFactoryFromRoot()
    {
        // Garantiza que el lease producido por el fixture NO comparte factory
        // con la root del composite (mismo principio que
        // WebIntegrationFixtureTests:Fixture_CreateAnonymousLeaseAsync_DerivesFactoryFromSharedRoot).
        await using var integrationFixture = new WebIntegrationFixture();
        await using var puestoFixture = new PuestoWebTestFixture();

        var lease = await puestoFixture.CreateAuthenticatedClientAsync(new FakePuestosApiClient());

        Assert.NotNull(lease);
        Assert.NotSame(integrationFixture.RootFactory, lease.Factory);
    }
}
