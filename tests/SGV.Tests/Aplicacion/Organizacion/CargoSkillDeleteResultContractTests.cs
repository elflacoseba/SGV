using System.Net;
using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

/// <summary>
/// Aprobación de contrato para <see cref="CargoSkillDeleteResult"/>.
///
/// El type ya existe (introducido en <c>9b4aac48 feat(aplicacion): add
/// CargoSkillDeleteResult for subresource delete contract</c>) y no se
/// modifica en su semántica original, pero la forma exacta del record es
/// el contrato público que consumen <c>CargoApiClient.DeleteSkillAsync</c>
/// y la futura Razor Page de PR3b. Estos tests blindan cuatro invariantes
/// del shape original y dos invariantes nuevos introducidos en el change
/// <c>2026-07-13-taxonomia-errores-commandresult</c>:
///
/// <list type="number">
///   <item>Existen exactamente cinco propiedades posicionales con los nombres
///         <c>Succeeded</c>, <c>StatusCode</c>, <c>Code</c>, <c>Message</c> y
///         <c>Categoria</c> (esta última agregada en el change #125).</item>
///   <item>Los tipos CLR coinciden con la firma pública del record.
///         <c>StatusCode</c> es nullable, <c>Code</c>/<c>Message</c> son
///         <see cref="string"/> nullable, <c>Succeeded</c> es <see cref="bool"/>
///         no-nullable, <c>Categoria</c> es <see cref="ErrorCategoria"/>.</item>
///   <item>El record se puede construir con <c>Succeeded=true</c> sin necesidad
///         de un <see cref="CargoSkillDeleteResult"/> pre-existente.</item>
///   <item>La propiedad <c>StatusCode</c> es un
///         <see cref="HttpStatusCode"/> (no un <see cref="int"/>) para que
///         Razor Pages pueda compararlo con <c>HttpStatusCode.NoContent</c>
///         sin ordinal juggling.</item>
///   <item><c>Succeeded=true</c> deja <c>Categoria</c> con su valor default
///         (<see cref="ErrorCategoria.NotFound"/>) porque un delete exitoso
///         no debería traer categoría de fallo.</item>
///   <item><c>Succeeded=false</c> permite poblar <c>Categoria</c> según el
///         status HTTP (matriz REQ-2 del spec
///         <c>commandresult-error-taxonomy</c>): 409→<c>Conflict</c>,
///         5xx→<c>Transport</c>, etc.</item>
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
    public void Record_ExposesFivePositionalPropertiesWithExpectedNames()
    {
        var properties = typeof(CargoSkillDeleteResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Categoria", "Code", "Message", "StatusCode", "Succeeded" }, properties);
    }

    [Fact]
    public void Record_PropertiesHaveExpectedClrTypes()
    {
        var type = typeof(CargoSkillDeleteResult);

        Assert.Equal(typeof(bool), type.GetProperty("Succeeded")!.PropertyType);
        Assert.Equal(typeof(HttpStatusCode?), type.GetProperty("StatusCode")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Code")!.PropertyType);
        Assert.Equal(typeof(string), type.GetProperty("Message")!.PropertyType);
        Assert.Equal(typeof(ErrorCategoria), type.GetProperty("Categoria")!.PropertyType);
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
            Message: null,
            Categoria: default);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
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

    [Fact]
    public void Record_SucceededFalse_CategoriaPobladaSegunStatus()
    {
        var conflict = new CargoSkillDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.Conflict,
            Code: "HabilidadEnUso",
            Message: "La habilidad está en uso",
            Categoria: ErrorCategoria.Conflict);

        Assert.Equal(ErrorCategoria.Conflict, conflict.Categoria);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var transport = new CargoSkillDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.BadGateway,
            Code: "TransportError",
            Message: "upstream no disponible",
            Categoria: ErrorCategoria.Transport);

        Assert.Equal(ErrorCategoria.Transport, transport.Categoria);
    }
}
