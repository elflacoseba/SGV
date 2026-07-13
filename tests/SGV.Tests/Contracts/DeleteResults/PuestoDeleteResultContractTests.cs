using System.Net;
using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Contracts.DeleteResults;

/// <summary>
/// Aprobación de contrato para <see cref="PuestoDeleteResult"/>.
///
/// Este record pasa por un cambio source-compatible en este Slice:
/// <c>StatusCode</c> migra de <see cref="HttpStatusCode"/> (non-nullable) a
/// <see cref="HttpStatusCode?"/> (nullable) para alinearse con los demás
/// <c>*DeleteResult</c> y absorber el caso "204 sin status code" sin
/// inconsistencias. Se agrega además la propiedad
/// <c>Categoria: ErrorCategoria</c>.
/// </summary>
public sealed class PuestoDeleteResultContractTests
{
    [Fact]
    public void Record_ExposesFivePositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(PuestoDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Categoria", "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(PuestoDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        // StatusCode ahora es HttpStatusCode? (nullable).
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
        Assert.Equal(typeof(ErrorCategoria), type.GetProperty("Categoria")!.PropertyType);
    }

    [Fact]
    public void Record_StatusCodeIsHttpStatusCode_NotRawInt()
    {
        var property = typeof(PuestoDeleteResult).GetProperty("StatusCode")!;

        Assert.Equal(typeof(HttpStatusCode), Nullable.GetUnderlyingType(property.PropertyType));
    }

    [Fact]
    public void Record_SucceededTrue_CategoriaDefaultsToExpectedValue()
    {
        var result = new PuestoDeleteResult(
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
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new PuestosApiClient(httpClient);

        var result = await client.DeleteAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Transport, result.Categoria);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal("TransportError", result.Code);
        Assert.Equal("El servicio no respondió correctamente. Intentá nuevamente.", result.Message);
    }
}
