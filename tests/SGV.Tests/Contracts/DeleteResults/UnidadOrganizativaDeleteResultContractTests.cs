using System.Net;
using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Contracts.DeleteResults;

/// <summary>
/// Aprobación de contrato para <see cref="UnidadOrganizativaDeleteResult"/>.
/// Ver shape equivalente al de <see cref="HabilidadDeleteResult"/>:
/// <c>Categoria: ErrorCategoria</c> agregado en este change.
/// </summary>
public sealed class UnidadOrganizativaDeleteResultContractTests
{
    [Fact]
    public void Record_ExposesFivePositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(UnidadOrganizativaDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Categoria", "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(UnidadOrganizativaDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
        Assert.Equal(typeof(ErrorCategoria), type.GetProperty("Categoria")!.PropertyType);
    }

    [Fact]
    public void Record_SucceededTrue_CategoriaDefaultsToExpectedValue()
    {
        var result = new UnidadOrganizativaDeleteResult(
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
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new UnidadOrganizativaApiClient(httpClient);

        var result = await client.DeleteAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Categoria);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("Unauthorized", result.Code);
        Assert.Equal("Su sesión expiró. Vuelva a iniciar sesión.", result.Message);
    }
}
