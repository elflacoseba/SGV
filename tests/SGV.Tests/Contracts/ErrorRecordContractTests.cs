using System.Reflection;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Aprobación de contrato para la propiedad <see cref="ErrorCategoria"/>
/// agregada a los seis <c>*Error</c> records de <c>SGV.Contracts</c>.
///
/// El cambio es source-compatible: el nuevo parámetro <c>Categoria</c>
/// se agrega con default <c>ErrorCategoria.Unexpected</c> para no romper
/// los call sites existentes (la fix-up de <c>SGV.Aplicacion</c> ocurre
/// en el Slice 4 vía T-4.5). Estos tests blindan que la propiedad existe
/// con el tipo CLR correcto y el valor por defecto esperado.
/// </summary>
public sealed class ErrorRecordContractTests
{
    [Fact]
    public void HabilidadError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(HabilidadError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new HabilidadError(
            Type: HabilidadErrorType.Validation,
            Code: "ValidationError",
            Message: "Datos inválidos",
            StatusCode: 400,
            Categoria: ErrorCategoria.Validation);

        Assert.Equal(ErrorCategoria.Validation, error.Categoria);
        Assert.Equal(ErrorCategoria.Validation, (ErrorCategoria)property.GetValue(error)!);
    }

    [Fact]
    public void HabilidadError_CategoriaDefaultsToUnexpected_WhenNotSpecified()
    {
        var error = new HabilidadError(
            Type: HabilidadErrorType.NotFound,
            Code: "NotFound",
            Message: "no encontrado");

        Assert.Equal(ErrorCategoria.Unexpected, error.Categoria);
    }

    [Fact]
    public void CargoError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(CargoError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new CargoError(
            Type: CargoErrorType.NotFound,
            Code: "CargoNoExiste",
            Message: "El cargo no existe",
            Categoria: ErrorCategoria.NotFound);

        Assert.Equal(ErrorCategoria.NotFound, error.Categoria);
    }

    [Fact]
    public void PuestoError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(PuestoError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new PuestoError(
            Type: PuestoErrorType.Validation,
            Code: "PuestoInvalido",
            Message: "datos inválidos",
            Categoria: ErrorCategoria.Validation);

        Assert.Equal(ErrorCategoria.Validation, error.Categoria);
    }

    [Fact]
    public void UnidadOrganizativaError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(UnidadOrganizativaError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new UnidadOrganizativaError(
            Type: UnidadOrganizativaErrorType.Conflict,
            Code: "UoCircular",
            Message: "referencia circular",
            Categoria: ErrorCategoria.Conflict);

        Assert.Equal(ErrorCategoria.Conflict, error.Categoria);
    }

    [Fact]
    public void CargoSkillError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(CargoSkillError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new CargoSkillError(
            Type: CargoSkillErrorType.Unauthorized,
            Code: "Unauthorized",
            Message: "sesión expirada",
            Categoria: ErrorCategoria.Unauthorized);

        Assert.Equal(ErrorCategoria.Unauthorized, error.Categoria);
    }

    [Fact]
    public void UsuarioError_ExposesCategoriaOfTypeErrorCategoria()
    {
        var property = typeof(UsuarioError).GetProperty("Categoria");

        Assert.NotNull(property);
        Assert.Equal(typeof(ErrorCategoria), property!.PropertyType);

        var error = new UsuarioError(
            Type: UsuarioErrorType.Unauthorized,
            Code: "Unauthorized",
            Message: "sesión expirada",
            Categoria: ErrorCategoria.Unauthorized);

        Assert.Equal(ErrorCategoria.Unauthorized, error.Categoria);
    }
}
