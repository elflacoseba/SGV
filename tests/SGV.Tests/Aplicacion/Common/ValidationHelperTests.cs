using FluentValidation.Results;
using SGV.Aplicacion.Common;
using Xunit;

namespace SGV.Tests.Aplicacion.Common;

/// <summary>
/// Unit tests for the centralized <see cref="ValidationHelper"/>.
///
/// Pre-issue-#102, <c>ValidationHelper</c> was scoped to
/// <c>SGV.Aplicacion.Personas.Comandos.Validaciones</c> and three other
/// service classes (<c>CargoServicioComandos</c>,
/// <c>UnidadOrganizativaServicioComandos</c>,
/// <c>HabilidadServicioComandos</c>, <c>PuestoServicioComandos</c>,
/// <c>OcupacionServicioComandos</c>) carried their own private copies of
/// <c>ToCamelCase</c> + <c>BuildFieldErrors</c>. After centralization all
/// callers consume this single helper; these tests pin the contract:
/// camelCase key formatting (matching the JSON casing of incoming HTTP
/// requests) and per-field grouping of FluentValidation failures.
/// </summary>
public class ValidationHelperTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Codigo", "codigo")]
    [InlineData("NombreCompleto", "nombreCompleto")]
    [InlineData("tipoUnidadOrganizativaId", "tipoUnidadOrganizativaId")] // already lowercase prefix
    [InlineData("P", "p")]
    [InlineData("p", "p")]
    public void ToCamelCase_ProducesExpectedCamelCase(string input, string expected)
    {
        Assert.Equal(expected, ValidationHelper.ToCamelCase(input));
    }

    [Fact]
    public void BuildFieldErrors_GroupsFailuresByCamelCasedPropertyName()
    {
        var failures = new[]
        {
            new ValidationFailure("Codigo", "El código es obligatorio."),
            new ValidationFailure("Codigo", "El código no puede superar 50 caracteres."),
            new ValidationFailure("Nombre", "El nombre es obligatorio."),
            new ValidationFailure("TipoUnidadOrganizativaId", "Tipo inválido.")
        };

        var result = ValidationHelper.BuildFieldErrors(failures);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "El código es obligatorio.", "El código no puede superar 50 caracteres." }, result["codigo"]);
        Assert.Equal(new[] { "El nombre es obligatorio." }, result["nombre"]);
        Assert.Equal(new[] { "Tipo inválido." }, result["tipoUnidadOrganizativaId"]);
    }

    [Fact]
    public void BuildFieldErrors_NoFailures_ReturnsEmptyDictionary()
    {
        var result = ValidationHelper.BuildFieldErrors(Array.Empty<ValidationFailure>());

        Assert.Empty(result);
    }
}