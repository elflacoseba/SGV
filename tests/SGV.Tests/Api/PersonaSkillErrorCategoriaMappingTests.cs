using System.Net;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Aprobación de la taxonomía <see cref="ErrorCategoria"/> consolidada
/// para <c>PersonaSkill*</c> (slice 1 / REQ-TAXO-02, SCENARIO-01, SCENARIO-02).
///
/// <para>
/// El backend actual distingue NotFound vs. Validation por el enum
/// <c>PersonaSkillErrorType</c>; el cambio alinea esa taxonomía con
/// <see cref="ErrorCategoria"/> y exige que <see cref="SGV.Contracts.Comun.ErrorCategoriaMappers"/>
/// (o el switch expression equivalente del lado API) traduzca
/// consistentemente al código HTTP observable. Estos tests son guards:
/// si alguien refactoriza el mapping, pierde una rama o cambia los
/// ordinales del enum, fallan antes de que el cambio llegue a Web.
/// </para>
/// </summary>
public sealed class PersonaSkillErrorCategoriaMappingTests
{
    [Fact]
    public void PersonaSkillError_NotFound_MapsToErrorCategoriaNotFound_404()
    {
        var type = PersonaSkillErrorType.NotFound;

        var categoria = ErrorCategoriaMappers.ToCategoria(type);

        Assert.Equal(ErrorCategoria.NotFound, categoria);
        Assert.Equal(404, MapCategoriaToHttp(categoria));
    }

    [Fact]
    public void PersonaSkillError_Validation_MapsToErrorCategoriaValidation_400()
    {
        var type = PersonaSkillErrorType.Validation;

        var categoria = ErrorCategoriaMappers.ToCategoria(type);

        Assert.Equal(ErrorCategoria.Validation, categoria);
        Assert.Equal(400, MapCategoriaToHttp(categoria));
    }

    [Fact]
    public void PersonaSkillError_ConstructionWithCategoria_ExposesCategoria()
    {
        // El servicio de aplicación debe poder fijar Categoria de forma
        // explícita al construir un PersonaSkillError; alineado con el
        // shape de CargoSkillError/HabilidadError vigentes.
        var error = new PersonaSkillError(
            Type: PersonaSkillErrorType.NotFound,
            Code: "PersonaNoEncontrada",
            Message: "La persona no existe.",
            StatusCode: 404,
            Categoria: ErrorCategoria.NotFound);

        Assert.Equal(ErrorCategoria.NotFound, error.Categoria);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("PersonaNoEncontrada", error.Code);
    }

    [Fact]
    public void PersonaSkillError_ConstructionWithoutCategoria_DefaultsToUnexpected()
    {
        var error = new PersonaSkillError(
            Type: PersonaSkillErrorType.Validation,
            Code: "DatosInvalidos",
            Message: "x");

        // Default back-compat con la firma previa del record (vive en
        // Contracts ahora); defaults preservan los call sites existentes.
        Assert.Equal(ErrorCategoria.Unexpected, error.Categoria);
        Assert.Null(error.StatusCode);
    }

    [Fact]
    public void PersonaSkillDeleteResult_ConstructionWithCategoria_ExposesCategoriaAndStatusCode()
    {
        // REQ-TAXO-02, SCENARIO-01/02: PersonaSkillDeleteResult debe
        // exponer Categoria (ErrorCategoria) y preservar StatusCode como
        // metadata (shape espejo de CargoSkillDeleteResult).
        var result = new PersonaSkillDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "AsignacionNoEncontrada",
            Message: "La asociación no existe.",
            Categoria: ErrorCategoria.NotFound);

        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("AsignacionNoEncontrada", result.Code);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void PersonaSkillDeleteResult_ConstructionWithoutCategoria_DefaultsToNotFound()
    {
        // Default conservador: al construir un DeleteResult sin Categoria
        // (i.e. el cliente no sabe mapear la causa), cae a NotFound —
        // mismo shape que CargoSkillDeleteResult.
        var result = new PersonaSkillDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "AsignacionNoEncontrada",
            Message: "La asociación no existe.");

        Assert.Equal(ErrorCategoria.NotFound, result.Categoria);
    }

    // Espejo mínimo de ApiResults.MapCategoria: vive en los tests para
    // no arrastrar SGV.Api a un proyecto xUnit. Mantiene el contrato
    // canónico ErrorCategoria→HTTP design §2.3.
    private static int MapCategoriaToHttp(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.Validation => 400,
        ErrorCategoria.NotFound => 404,
        ErrorCategoria.Conflict => 409,
        ErrorCategoria.Unauthorized => 401,
        ErrorCategoria.Forbidden => 403,
        ErrorCategoria.Transport => 503,
        ErrorCategoria.Unexpected => 500,
        _ => throw new System.ArgumentOutOfRangeException(nameof(categoria)),
    };
}
