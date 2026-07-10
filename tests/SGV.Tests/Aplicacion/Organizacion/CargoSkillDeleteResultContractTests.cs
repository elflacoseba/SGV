using System.Net;
using System.Reflection;
using SGV.Contracts.Organizacion.Comandos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

/// <summary>
/// Aprobación de contrato para <see cref="CargoSkillDeleteResult"/>.
///
/// El type ya existe (introducido en <c>9b4aac48 feat(aplicacion): add
/// CargoSkillDeleteResult for subresource delete contract</c>) y no se
/// modifica en esta ronda, pero la forma exacta del record es el contrato
/// público que consumen <c>CargoApiClient.DeleteSkillAsync</c> y la futura
/// Razor Page de PR3b. Estos tests blindan cuatro invariantes:
///
/// <list type="number">
///   <item>Existen exactamente cuatro propiedades posicionales con los nombres
///         <c>Succeeded</c>, <c>StatusCode</c>, <c>Code</c> y <c>Message</c>.</item>
///   <item>Los tipos CLR coinciden con la firma pública del record.
///         <c>StatusCode</c> es nullable, <c>Code</c>/<c>Message</c> son
///         <see cref="string"/> nullable, <c>Succeeded</c> es <see cref="bool"/>
///         no-nullable.</item>
///   <item>El record se puede construir con <c>Succeeded=true</c> sin necesidad
///         de un <see cref="CargoSkillDeleteResult"/> pre-existente.</item>
///   <item>La propiedad <c>StatusCode</c> es un
///         <see cref="HttpStatusCode"/> (no un <see cref="int"/>) para que
///         Razor Pages pueda compararlo con <c>HttpStatusCode.NoContent</c>
///         sin ordinal juggling.</item>
/// </list>
///
/// Si alguien futuro borra una propiedad, cambia un nombre o mete un alias,
/// estos tests fallan y exponen la regresión antes de que el cambio llegue a
/// la Razor Page. Esto es contract approval testing conforme al patrón
/// "approval tests" del strict-tdd: capturas el comportamiento actual con
/// assertions concretos, sin tocar producción.
/// </summary>
public class CargoSkillDeleteResultContractTests
{
    [Fact]
    public void Record_ExposesFourPositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(CargoSkillDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(CargoSkillDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
    }

    [Fact]
    public void Record_CanBeConstructedWithSucceededTrue()
    {
        // Blindaje del camino feliz: la Razor Page trata Succeeded=true como
        // éxito sin necesidad de inspeccionar los demás campos. Si el record
        // pasara a ser class o se le quitara el ctor posicional, esto rompe.
        var result = new CargoSkillDeleteResult(
            Succeeded: true,
            StatusCode: HttpStatusCode.NoContent,
            Code: null,
            Message: null);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Record_StatusCodeIsHttpStatusCode_NotRawInt()
    {
        // Defiende la elección tipada para que los call sites no tengan que
        // hacer cast a (int) al comparar contra HttpStatusCode.NoContent en
        // la Razor Page de PR3b.
        var property = typeof(CargoSkillDeleteResult).GetProperty("StatusCode")!;

        Assert.Equal(typeof(HttpStatusCode), Nullable.GetUnderlyingType(property.PropertyType));
    }
}
