using System.Net;
using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Contracts.DeleteResults;

/// <summary>
/// Aprobación de contrato para <see cref="HabilidadDeleteResult"/>.
///
/// El record vive en <c>src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs</c>
/// (consumido por <c>HabilidadApiClient.DeleteAsync</c> y la futura
/// migración a la nueva taxonomía). En este change se le agrega la
/// propiedad <c>Categoria: ErrorCategoria</c> para alinear la forma con
/// los demás <c>*DeleteResult</c> y permitir que las Razor Pages
/// ramifiquen por categoría en lugar de comparar <c>StatusCode</c>
/// contra constantes HTTP.
/// </summary>
public sealed class HabilidadDeleteResultContractTests
{
    [Fact]
    public void Record_ExposesFivePositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(HabilidadDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Categoria", "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(HabilidadDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
        Assert.Equal(typeof(ErrorCategoria), type.GetProperty("Categoria")!.PropertyType);
    }

    [Fact]
    public void Record_SucceededTrue_CategoriaDefaultsToExpectedValue()
    {
        // Succeeded=true → Categoria queda con default(ErrorCategoria) que
        // es NotFound (ordinal 0). Esta convención se mantiene porque un
        // delete exitoso no debería traer categoría de fallo.
        var result = new HabilidadDeleteResult(
            Succeeded: true,
            StatusCode: HttpStatusCode.NoContent,
            Code: null,
            Message: null,
            Categoria: default);

        Assert.True(result.Succeeded);
        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
    }

    [Fact]
    public async Task Record_SucceededFalse_CategoriaPobladaSegunStatus()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HabilidadApiClient(
            httpClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HabilidadApiClient>.Instance);

        var result = await client.DeleteAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Conflict, result.Categoria);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("Conflict", result.Code);
        Assert.Equal("Conflicto.", result.Message);
    }
}
