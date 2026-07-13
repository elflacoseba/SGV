using System.Net;
using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Contracts.DeleteResults;

/// <summary>
/// Aprobación de contrato para <see cref="CargoDeleteResult"/>.
/// Ver shape equivalente al de <see cref="HabilidadDeleteResult"/>:
/// <c>Categoria: ErrorCategoria</c> agregado en este change.
/// </summary>
public sealed class CargoDeleteResultContractTests
{
    [Fact]
    public void Record_ExposesFivePositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(CargoDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Categoria", "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(CargoDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
        Assert.Equal(typeof(ErrorCategoria), type.GetProperty("Categoria")!.PropertyType);
    }

    [Fact]
    public void Record_SucceededTrue_CategoriaDefaultsToExpectedValue()
    {
        var result = new CargoDeleteResult(
            Succeeded: true,
            StatusCode: HttpStatusCode.NoContent,
            Code: null,
            Message: null,
            Categoria: default);

        Assert.True(result.Succeeded);
        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
    }

    [Fact]
    public void Record_SucceededFalse_CategoriaPobladaSegunStatus()
    {
        var notFound = new CargoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "CargoNoExiste",
            Message: "El cargo no existe",
            Categoria: ErrorCategoria.NotFound);

        Assert.Equal(ErrorCategoria.NotFound, notFound.Categoria);

        var unexpected = new CargoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.InternalServerError,
            Code: "Unexpected",
            Message: "Error inesperado",
            Categoria: ErrorCategoria.Unexpected);

        Assert.Equal(ErrorCategoria.Unexpected, unexpected.Categoria);
    }
}
