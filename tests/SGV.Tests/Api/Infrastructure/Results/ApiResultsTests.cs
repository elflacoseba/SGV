using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using Xunit;

namespace SGV.Tests.Api.Infrastructure.Results;

/// <summary>
/// Unit tests for <see cref="ApiResults"/>. The helper centralizes the
/// mapping of typed application errors (CargoError, HabilidadError, …) to
/// ASP.NET Core <see cref="ProblemDetails"/> /
/// <see cref="ValidationProblemDetails"/> responses.
///
/// Each assertion here fixes ONE cell of the error-category ↔ HTTP-status
/// matrix. Replacing the helper with a different mapping will fail the
/// affected case, which is the regression net for the centralization done
/// in issue #102.
/// </summary>
public class ApiResultsTests
{
    // ---- CargoError (paradigm case) ----

    [Fact]
    public void ToProblemResult_CargoNotFound_Returns404ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
        Assert.Equal("CargoNoEncontrado", problem.Title);
        Assert.Equal("El cargo no existe.", problem.Detail);
        Assert.Equal("https://httpstatuses.com/404", problem.Type);
    }

    [Fact]
    public void ToProblemResult_CargoConflict_Returns409ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.Conflict, "CodigoDuplicado", "Ya existe un cargo activo con el mismo código."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
        Assert.Equal("CodigoDuplicado", problem.Title);
        Assert.Equal("Ya existe un cargo activo con el mismo código.", problem.Detail);
    }

    [Fact]
    public void ToProblemResult_CargoValidation_Returns400ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("DatosInvalidos", problem.Title);
    }

    [Fact]
    public void ToValidationProblemResult_CargoWithFieldErrors_Returns400ValidationProblemDetailsWithErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código es obligatorio." },
            ["nombre"] = new[] { "El nombre es obligatorio.", "El nombre no puede superar 200 caracteres." }
        };

        var actionResult = ApiResults.ToValidationProblemResult(
            new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores."),
            fieldErrors);

        var problem = Assert.IsType<BadRequestObjectResult>(actionResult).Value as ValidationProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("DatosInvalidos", problem.Title);
        Assert.Equal("Uno o más campos contienen errores.", problem.Detail);
        Assert.Equal(2, problem.Errors.Count);
        Assert.Equal("El código es obligatorio.", problem.Errors["codigo"].Single());
        Assert.Equal(2, problem.Errors["nombre"].Length);
    }

    [Fact]
    public void ToValidationProblemResult_CargoWithNullFieldErrors_Returns400ValidationProblemDetailsWithEmptyErrors()
    {
        // Cuando el servicio no devuelve errores por campo pero la ruta del
        // controller ya bifurc a ValidationProblemDetails (p.ej. CargoSkill),
        // el body debe seguir siendo un ValidationProblemDetails con `errors`
        // vacío. Antes de la centralización, esa rama generaba un body
        // ProblemDetails genérico; la helper unifica el shape.
        var actionResult = ApiResults.ToValidationProblemResult(
            new CargoSkillError(CargoSkillErrorType.Validation, "EmptyBody", "El servidor respondió 200 sin payload."),
            fieldErrors: null);

        var problem = Assert.IsType<BadRequestObjectResult>(actionResult).Value as ValidationProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("EmptyBody", problem.Title);
        Assert.Empty(problem.Errors);
    }

    // ---- HabilidadError ----

    [Fact]
    public void ToProblemResult_HabilidadConflict_Returns409ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new HabilidadError(HabilidadErrorType.Conflict, "CodigoDuplicado", "Ya existe una habilidad activa con el mismo código."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
        Assert.Equal("CodigoDuplicado", problem.Title);
    }

    // ---- PuestoError ----

    [Fact]
    public void ToProblemResult_PuestoNotFound_Returns404ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new PuestoError(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
        Assert.Equal("PuestoNoEncontrado", problem.Title);
    }

    // ---- UnidadOrganizativaError ----

    [Fact]
    public void ToProblemResult_UnidadOrganizativaConflict_Returns409ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "UnidadConHijasActivas", "La unidad tiene hijas activas."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
    }

    // ---- OcupacionError ----

    [Fact]
    public void ToProblemResult_OcupacionConflict_Returns409ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new OcupacionError(ErrorCategoria.Conflict, "PuestoYaOcupado", "El puesto ya está ocupado."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
    }

    // ---- PersonaError ----

    [Fact]
    public void ToProblemResult_PersonaNotFound_Returns404ProblemDetails()
    {
        var actionResult = ApiResults.ToProblemResult(
            new PersonaError(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
    }

    // ---- Matrix (paradigm): the same shape must apply to every enum ----

    [Theory]
    [InlineData("CargoNoEncontrado", "NotFound", (int)HttpStatusCode.NotFound)]
    [InlineData("CargoConPuestosActivos", "Conflict", (int)HttpStatusCode.Conflict)]
    [InlineData("DatosInvalidos", "Validation", (int)HttpStatusCode.BadRequest)]
    public void ToProblemResult_CargoErrorMatrix_MapsEnumToStatusCode(
        string code, string typeName, int expectedStatusCode)
    {
        var type = typeName switch
        {
            "NotFound" => CargoErrorType.NotFound,
            "Conflict" => CargoErrorType.Conflict,
            "Validation" => CargoErrorType.Validation,
            _ => throw new ArgumentOutOfRangeException(nameof(typeName))
        };

        var actionResult = ApiResults.ToProblemResult(new CargoError(type, code, "msg"));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(expectedStatusCode, problem!.Status);
        Assert.Equal(code, problem.Title);
        Assert.Equal("msg", problem.Detail);
        Assert.Equal($"https://httpstatuses.com/{expectedStatusCode}", problem.Type);
    }

    [Theory]
    [InlineData(ErrorCategoria.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCategoria.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCategoria.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCategoria.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCategoria.Transport, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorCategoria.Unexpected, StatusCodes.Status500InternalServerError)]
    public void ToProblemResult_ErrorCategoriaMatrix_MapsCategoriaToStatusCode(
        ErrorCategoria categoria,
        int expectedStatusCode)
    {
        var error = new CargoError(
            CargoErrorType.Validation,
            "CategoriaError",
            "mensaje",
            StatusCode: expectedStatusCode,
            Categoria: categoria);

        var objectResult = Assert.IsType<ObjectResult>(ApiResults.ToProblemResult(error));
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        Assert.Equal(expectedStatusCode, problem.Status);
    }

    [Fact]
    public void ToValidationProblemResult_AllEnums_ProduceStatus400WithSameTitleAndDetail()
    {
        // La forma del body de un ValidationProblemDetails es uniforme: status=400,
        // title=code, detail=message. Esa uniformidad es la razón de ser de la
        // helper. Si en el futuro algún módulo quiere un status distinto para
        // validation (p.ej. 422 Unprocessable Entity), este test debe fallar y
        // forzar la decisión explícita.
        var fieldErrors = new Dictionary<string, string[]> { ["x"] = new[] { "y" } };

        var cases = new (ActionResult Action, string ExpectedTitle, string ExpectedDetail)[]
        {
            (ApiResults.ToValidationProblemResult(
                new CargoError(CargoErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new HabilidadError(HabilidadErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new PuestoError(PuestoErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new OcupacionError(ErrorCategoria.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new PersonaError(PersonaErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
            (ApiResults.ToValidationProblemResult(
                new CargoSkillError(CargoSkillErrorType.Validation, "DatosInvalidos", "msg"), fieldErrors), "DatosInvalidos", "msg"),
        };

        foreach (var (action, expectedTitle, expectedDetail) in cases)
        {
            var problem = Assert.IsType<BadRequestObjectResult>(action).Value as ValidationProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
            Assert.Equal(expectedTitle, problem.Title);
            Assert.Equal(expectedDetail, problem.Detail);
            Assert.Single(problem.Errors);
        }
    }

    // ---- UsuarioError (previously uncovered mapping) ----

    [Theory]
    [InlineData(UsuarioErrorType.NotFound, (int)HttpStatusCode.NotFound)]
    [InlineData(UsuarioErrorType.Conflict, (int)HttpStatusCode.Conflict)]
    [InlineData(UsuarioErrorType.Unauthorized, (int)HttpStatusCode.Unauthorized)]
    [InlineData(UsuarioErrorType.Validation, (int)HttpStatusCode.BadRequest)]
    public void ToProblemResult_UsuarioErrorMatrix_MapsEnumToStatusCode(
        UsuarioErrorType type, int expectedStatusCode)
    {
        var actionResult = ApiResults.ToProblemResult(
            new UsuarioError(type, "UsuarioCodigo", "mensaje"));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal(expectedStatusCode, problem!.Status);
        Assert.Equal("UsuarioCodigo", problem.Title);
        Assert.Equal("mensaje", problem.Detail);
        Assert.Equal($"https://httpstatuses.com/{expectedStatusCode}", problem.Type);
    }

    // ---- traceId contract (regresión de issue #102) ----
    //
    // Pre-#102 los controllers usaban ControllerBase.Problem(), que a través
    // del ProblemDetailsFactory por defecto adjuntaba una extensión "traceId".
    // La centralización en ApiResults la perdía. El helper la reincorpora
    // cuando se le pasa el HttpContext, replicando la fuente del factory:
    // Activity.Current?.Id ?? HttpContext.TraceIdentifier.

    [Fact]
    public void ToProblemResult_WithoutHttpContext_DoesNotAttachTraceId()
    {
        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.False(problem!.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public void ToProblemResult_WithHttpContext_AttachesTraceIdFromTraceIdentifier()
    {
        // Neutralizamos cualquier Activity ambiental del runner para que la
        // fuente del traceId sea, de forma determinista, TraceIdentifier.
        Activity.Current = null;
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-abc-123" };

        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."),
            httpContext);

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.True(problem!.Extensions.TryGetValue("traceId", out var traceId));
        Assert.Equal("trace-abc-123", traceId);
    }

    [Fact]
    public void ToProblemResult_WithActiveActivity_PrefersActivityIdOverTraceIdentifier()
    {
        using var activity = new Activity("apiresults-test").Start();
        var httpContext = new DefaultHttpContext { TraceIdentifier = "ignored-when-activity-present" };

        var actionResult = ApiResults.ToProblemResult(
            new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."),
            httpContext);

        var problem = Assert.IsType<ObjectResult>(actionResult).Value as ProblemDetails;
        Assert.NotNull(problem);
        Assert.True(problem!.Extensions.TryGetValue("traceId", out var traceId));
        Assert.Equal(activity.Id, traceId);
    }

    [Fact]
    public void ToValidationProblemResult_WithHttpContext_AttachesTraceId()
    {
        Activity.Current = null;
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-xyz-789" };
        var fieldErrors = new Dictionary<string, string[]> { ["codigo"] = new[] { "obligatorio" } };

        var actionResult = ApiResults.ToValidationProblemResult(
            new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores."),
            fieldErrors,
            httpContext);

        var problem = Assert.IsType<BadRequestObjectResult>(actionResult).Value as ValidationProblemDetails;
        Assert.NotNull(problem);
        Assert.True(problem!.Extensions.TryGetValue("traceId", out var traceId));
        Assert.Equal("trace-xyz-789", traceId);
    }
}