using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Seam tests for the web layer of Habilidades (Slice 2):
///   - 2.1 record shape of <see cref="HabilidadListItemViewModel"/>,
///     <see cref="HabilidadListQuery"/> and <see cref="HabilidadDeleteResult"/>;
///   - 2.4 <see cref="IHabilidadApiClient"/> is resolvable from the production
///     service collection registered in <c>Program.cs</c>;
///   - 2.4 <see cref="SgvWebApplicationFactory.WithOverrides"/> swaps the
///     production client for a fake.
///
/// Se une a <c>[Collection("WebIntegration")]</c> para que las leases que
/// sostienen el host de pruebas pertenezcan al composite compartido y no
/// queden factories huérfanas fuera del scope.
/// </summary>
[Collection("WebIntegration")]
public sealed class HabilidadWebSeamTests
{
    private readonly WebIntegrationFixture _fixture;

    public HabilidadWebSeamTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public void HabilidadListItemViewModel_Constructor_ExposesAllProperties()
    {
        var id = Guid.NewGuid();
        var vm = new HabilidadListItemViewModel(id, "H-001", "Liderazgo", "Descripción", "Conductual");

        Assert.Equal(id, vm.Id);
        Assert.Equal("H-001", vm.Codigo);
        Assert.Equal("Liderazgo", vm.Nombre);
        Assert.Equal("Descripción", vm.Descripcion);
        Assert.Equal("Conductual", vm.Categoria);
    }

    [Fact]
    public void HabilidadListQuery_Constructor_ExposesAllProperties()
    {
        var query = new HabilidadListQuery(Page: 2, PageSize: 25, Search: "lid", Sort: "nombre", Status: "eliminadas");

        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("lid", query.Search);
        Assert.Equal("nombre", query.Sort);
        Assert.Equal("eliminadas", query.Status);
    }

    [Fact]
    public void HabilidadDeleteResult_Constructor_ExposesAllProperties()
    {
        var result = new HabilidadDeleteResult(true, HttpStatusCode.NoContent, "Code", "Message");

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal("Code", result.Code);
        Assert.Equal("Message", result.Message);
    }

    [Fact]
    public void HabilidadInputModel_Defaults_CodigoEsVacioYCategoriaEsNull()
    {
        var input = new HabilidadInputModel();

        Assert.Equal(string.Empty, input.Codigo);
        Assert.Equal(string.Empty, input.Nombre);
        Assert.Null(input.Categoria);
        Assert.Null(input.Descripcion);
    }

    [Fact]
    public void HabilidadInputModel_LongitudesReflejanDominio()
    {
        // 50 (Codigo), 200 (Nombre), 100 (Categoria), 1000 (Descripcion) son
        // las longitudes que fija la entidad de dominio.
        var input = new HabilidadInputModel();
        var props = typeof(HabilidadInputModel).GetProperties();
        var codigoAttr = props.First(p => p.Name == nameof(input.Codigo))
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.StringLengthAttribute>()
            .Single();
        Assert.Equal(50, codigoAttr.MaximumLength);

        var nombreAttr = props.First(p => p.Name == nameof(input.Nombre))
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.StringLengthAttribute>()
            .Single();
        Assert.Equal(200, nombreAttr.MaximumLength);

        var categoriaAttr = props.First(p => p.Name == nameof(input.Categoria))
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.StringLengthAttribute>()
            .Single();
        Assert.Equal(100, categoriaAttr.MaximumLength);

        var descAttr = props.First(p => p.Name == nameof(input.Descripcion))
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.StringLengthAttribute>()
            .Single();
        Assert.Equal(1000, descAttr.MaximumLength);
    }

    [Fact]
    public async Task ProductionRegistration_ResolvesHabilidadApiClient()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();
        using var scope = lease.Factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IHabilidadApiClient>();

        Assert.NotNull(client);
        Assert.IsType<HabilidadApiClient>(client);
    }

    [Fact]
    public async Task WithOverrides_HabilidadApiClient_SwapsToFakeImplementation()
    {
        var fake = new FakeHabilidadApiClient
        {
            DeleteResult = new HabilidadDeleteResult(
                Succeeded: true,
                StatusCode: HttpStatusCode.NoContent,
                Code: null,
                Message: null)
        };

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(fake);
        using var scope = lease.Factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IHabilidadApiClient>();

        Assert.Same(fake, resolved);
    }
}