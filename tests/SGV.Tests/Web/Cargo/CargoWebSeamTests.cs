using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Red-first seam tests for tasks 1.5, 1.6 and 1.7 of PR 1:
///   - 1.5 record shape of <see cref="CargoListItemViewModel"/>,
///     <see cref="CargoListQuery"/> and <see cref="CargoDeleteResult"/>;
///   - 1.6 typed <see cref="ICargoApiClient"/> is resolvable from the production
///     service collection registered in <c>Program.cs</c>;
///   - 1.7 <see cref="SgvWebApplicationFactory.WithOverrides"/> swaps the
///     production client for a fake and the fake translates to the expected
///     <see cref="CargoDeleteResult"/> outcomes.
/// </summary>
public class CargoWebSeamTests
{
    [Fact]
    public void CargoListItemViewModel_Constructor_ExposesAllProperties()
    {
        var id = Guid.NewGuid();
        var vm = new CargoListItemViewModel(id, "C-001", "Analista", "Desc", "Junior");

        Assert.Equal(id, vm.Id);
        Assert.Equal("C-001", vm.Codigo);
        Assert.Equal("Analista", vm.Nombre);
        Assert.Equal("Desc", vm.Descripcion);
        Assert.Equal("Junior", vm.Nivel);
    }

    [Fact]
    public void CargoListQuery_Constructor_ExposesAllProperties()
    {
        var query = new CargoListQuery(Page: 2, PageSize: 25, Search: "ana", Sort: "nombre");

        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("ana", query.Search);
        Assert.Equal("nombre", query.Sort);
    }

    [Fact]
    public void CargoDeleteResult_Constructor_ExposesAllProperties()
    {
        var result = new CargoDeleteResult(true, HttpStatusCode.NoContent, "Code", "Message");

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal("Code", result.Code);
        Assert.Equal("Message", result.Message);
    }

    [Fact]
    public void ProductionRegistration_ResolvesCargoApiClient()
    {
        using var factory = new SgvWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<ICargoApiClient>();

        Assert.NotNull(client);
        Assert.IsType<CargoApiClient>(client);
    }

    [Fact]
    public void WithOverrides_CargoApiClient_SwapsToFakeImplementation()
    {
        var fake = new FakeCargoApiClient
        {
            DeleteResult = new CargoDeleteResult(
                Succeeded: true,
                StatusCode: HttpStatusCode.NoContent,
                Code: null,
                Message: null)
        };

        using var factory = new SgvWebApplicationFactory().WithOverrides(cargoApiClient: fake);
        using var scope = factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<ICargoApiClient>();

        Assert.Same(fake, resolved);
    }

    [Fact]
    public async Task WithOverrides_CargoApiClient_DefaultDeleteAsync_ReturnsSuccess()
    {
        var fake = new FakeCargoApiClient();
        var id = Guid.NewGuid();

        var result = await fake.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Contains(id, fake.DeleteCalls);
    }

    [Fact]
    public async Task WithOverrides_CargoApiClient_ConfiguredDeleteResult_IsReturned()
    {
        var fake = new FakeCargoApiClient
        {
            DeleteResult = new CargoDeleteResult(
                Succeeded: false,
                StatusCode: HttpStatusCode.Conflict,
                Code: "CargoConPuestosActivos",
                Message: "El cargo tiene puestos activos")
        };
        var id = Guid.NewGuid();

        var result = await fake.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CargoConPuestosActivos", result.Code);
        Assert.Equal("El cargo tiene puestos activos", result.Message);
        Assert.Contains(id, fake.DeleteCalls);
    }
}