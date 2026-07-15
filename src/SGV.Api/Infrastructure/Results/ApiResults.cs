using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Api.Infrastructure.Results;

/// <summary>
/// Centralizes the mapping of typed application errors to ASP.NET Core
/// <see cref="ProblemDetails"/> /
/// <see cref="ValidationProblemDetails"/> responses.
/// </summary>
/// <remarks>
/// <para>
/// Pre-issue-#102 each controller had its own private
/// <c>ToProblemResult</c> / <c>ToValidationProblemResult</c> pair that
/// reproduced the same status-code switch and the same
/// <c>ValidationProblemDetails</c> shape. The helpers were drifting: the
/// <c>Title</c>, <c>Detail</c>, <c>Type</c> and the <c>errors</c> payload
/// already diverged in subtle ways (e.g. CargoSkill emitted a generic
/// <c>ProblemDetails</c> for the 400-without-errors branch instead of a
/// <c>ValidationProblemDetails</c>, which forced the web client to special
/// case it). This class is the single source of truth for the
/// error-category → HTTP-status matrix and for the wire shape of both
/// <see cref="ProblemDetails"/> variants.
/// </para>
/// <para>
/// Controllers call exactly one line and forward their <see cref="HttpContext"/>
/// so the helper can re-attach the <c>traceId</c> extension that
/// <see cref="ControllerBase.Problem(string?, string?, int?, string?, string?)"/>
/// produced before issue #102:
/// <code>
/// return ApiResults.ToProblemResult(result.Error!, HttpContext);
/// return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);
/// </code>
/// No more per-controller duplication, no more drift between modules.
/// </para>
/// <para>
/// The <see cref="HttpContext"/> argument is optional: when omitted (unit
/// tests that only pin the status/title/detail matrix) the body is built
/// without <c>traceId</c>. When supplied, <c>traceId</c> is populated the
/// same way the default <c>ProblemDetailsFactory</c> does
/// (<see cref="Activity.Current"/> id, falling back to
/// <see cref="HttpContext.TraceIdentifier"/>), preserving the observable
/// wire contract for both <see cref="ProblemDetails"/> and
/// <see cref="ValidationProblemDetails"/>.
/// </para>
/// </remarks>
public static class ApiResults
{
    private const string ProblemTypeBaseUri = "https://httpstatuses.com/";

    /// <summary>
    /// Builds a <see cref="ProblemDetails"/> from a <see cref="CargoError"/>,
    /// selecting the HTTP status from <see cref="CargoErrorType"/>.
    /// </summary>
    public static ActionResult ToProblemResult(CargoError error, HttpContext? httpContext = null)
        => BuildProblem(MapCargoStatus(error), error.Code, error.Message, httpContext);

    /// <summary>
    /// Builds a <see cref="ValidationProblemDetails"/> for a <see cref="CargoError"/>.
    /// Always returns <c>400 Bad Request</c>; if <paramref name="fieldErrors"/>
    /// is non-empty it is copied verbatim into the <c>errors</c> payload,
    /// otherwise the body still ships as a <see cref="ValidationProblemDetails"/>
    /// with an empty <c>errors</c> dictionary so clients can rely on a single
    /// shape for any 400 returned by write endpoints.
    /// </summary>
    public static ActionResult ToValidationProblemResult(
        CargoError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>
    /// Builds a <see cref="ProblemDetails"/> for a <see cref="CargoSkillError"/>.
    /// CargoSkill only emits 400/401/403/404/409/5xx, but the controller
    /// forwards every non-success through this single entry point.
    /// </summary>
    public static ActionResult ToProblemResult(CargoSkillError error, HttpContext? httpContext = null)
        => BuildProblem(MapCargoSkillStatus(error), error.Code, error.Message, httpContext);

    /// <summary>
    /// Builds a <see cref="ValidationProblemDetails"/> for a
    /// <see cref="CargoSkillError"/>. Preserves the historical behavior of
    /// emitting a <c>ValidationProblemDetails</c> (with <c>errors</c> empty)
    /// for the 400-without-fieldErrors branch — previously a generic
    /// <c>ProblemDetails</c>, now unified so the web client has a single
    /// shape to parse.
    /// </summary>
    public static ActionResult ToValidationProblemResult(
        CargoSkillError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="HabilidadError"/>.</summary>
    public static ActionResult ToProblemResult(HabilidadError error, HttpContext? httpContext = null)
        => BuildProblem(MapHabilidadStatus(error), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ValidationProblemDetails"/> for a <see cref="HabilidadError"/>.</summary>
    public static ActionResult ToValidationProblemResult(
        HabilidadError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="PuestoError"/>.</summary>
    public static ActionResult ToProblemResult(PuestoError error, HttpContext? httpContext = null)
        => BuildProblem(MapPuestoStatus(error), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ValidationProblemDetails"/> for a <see cref="PuestoError"/>.</summary>
    public static ActionResult ToValidationProblemResult(
        PuestoError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="UnidadOrganizativaError"/>.</summary>
    public static ActionResult ToProblemResult(UnidadOrganizativaError error, HttpContext? httpContext = null)
        => BuildProblem(MapUnidadOrganizativaStatus(error), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ValidationProblemDetails"/> for a <see cref="UnidadOrganizativaError"/>.</summary>
    public static ActionResult ToValidationProblemResult(
        UnidadOrganizativaError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for an <see cref="OcupacionError"/>.</summary>
    public static ActionResult ToProblemResult(OcupacionError error, HttpContext? httpContext = null)
        => BuildProblem(MapOcupacionStatus(error.Type), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ValidationProblemDetails"/> for an <see cref="OcupacionError"/>.</summary>
    public static ActionResult ToValidationProblemResult(
        OcupacionError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="PersonaError"/>.</summary>
    public static ActionResult ToProblemResult(PersonaError error, HttpContext? httpContext = null)
        => BuildProblem(MapPersonaStatus(error.Type), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ValidationProblemDetails"/> for a <see cref="PersonaError"/>.</summary>
    public static ActionResult ToValidationProblemResult(
        PersonaError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext = null)
        => BuildValidationProblem(error.Code, error.Message, fieldErrors, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="PersonaSkillError"/>.</summary>
    public static ActionResult ToProblemResult(PersonaSkillError error, HttpContext? httpContext = null)
        => BuildProblem(MapPersonaSkillStatus(error.Type), error.Code, error.Message, httpContext);

    /// <summary>Builds a <see cref="ProblemDetails"/> for a <see cref="UsuarioError"/>.</summary>
    public static ActionResult ToProblemResult(UsuarioError error, HttpContext? httpContext = null)
        => BuildProblem(MapUsuarioStatus(error), error.Code, error.Message, httpContext);

    // ---- Internal builders + per-enum mappers ----

    /// <summary>
    /// Produces the wire response. <paramref name="statusCode"/> must be a
    /// valid HTTP status; <paramref name="title"/> maps to the ProblemDetails
    /// <c>title</c>; <paramref name="detail"/> maps to <c>detail</c>; the
    /// <c>type</c> URI follows the <c>https://httpstatuses.com/&lt;code&gt;</c>
    /// convention used historically by every controller.
    /// </summary>
    private static ActionResult BuildProblem(int statusCode, string title, string detail, HttpContext? httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"{ProblemTypeBaseUri}{statusCode}"
        };
        ApplyTraceId(problem, httpContext);
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>
    /// Produces a <see cref="ValidationProblemDetails"/> with status 400.
    /// <paramref name="fieldErrors"/> is copied verbatim when non-null; when
    /// it is null or empty the <c>errors</c> dictionary is initialized
    /// empty so the client always sees a <see cref="ValidationProblemDetails"/>
    /// shape (never a plain <see cref="ProblemDetails"/>) for write endpoints.
    /// </summary>
    private static ActionResult BuildValidationProblem(
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? fieldErrors,
        HttpContext? httpContext)
    {
        var modelState = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (fieldErrors is not null)
        {
            foreach (var kvp in fieldErrors)
            {
                modelState[kvp.Key] = kvp.Value;
            }
        }

        var problem = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail,
            Type = $"{ProblemTypeBaseUri}{StatusCodes.Status400BadRequest}"
        };
        ApplyTraceId(problem, httpContext);
        return new BadRequestObjectResult(problem);
    }

    /// <summary>
    /// Re-attaches the <c>traceId</c> extension the default
    /// <c>ProblemDetailsFactory</c> produced for
    /// <see cref="ControllerBase.Problem(string?, string?, int?, string?, string?)"/>
    /// before issue #102 centralized the mapping here. When
    /// <paramref name="httpContext"/> is null (pure unit tests over the
    /// status/title/detail matrix) no extension is added.
    /// </summary>
    private static void ApplyTraceId(ProblemDetails problem, HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return;
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }
    }

    private static int MapCategoria(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.Validation => StatusCodes.Status400BadRequest,
        ErrorCategoria.NotFound => StatusCodes.Status404NotFound,
        ErrorCategoria.Conflict => StatusCodes.Status409Conflict,
        ErrorCategoria.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategoria.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategoria.Transport => StatusCodes.Status503ServiceUnavailable,
        ErrorCategoria.Unexpected => StatusCodes.Status500InternalServerError,
        _ => throw new SwitchExpressionException(categoria)
    };

    private static int MapCargoStatus(CargoError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapCargoStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapCargoStatus(CargoErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static int MapCargoSkillStatus(CargoSkillError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapCargoSkillStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapCargoSkillStatus(CargoSkillErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static int MapHabilidadStatus(HabilidadError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapHabilidadStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapHabilidadStatus(HabilidadErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static int MapPuestoStatus(PuestoError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapPuestoStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapPuestoStatus(PuestoErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static int MapUnidadOrganizativaStatus(UnidadOrganizativaError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapUnidadOrganizativaStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapUnidadOrganizativaStatus(UnidadOrganizativaErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static int MapOcupacionStatus(OcupacionErrorType type)
        => MapCategoria(ToCategoria(type));

    private static int MapPersonaStatus(PersonaErrorType type)
        => MapCategoria(ToCategoria(type));

    private static int MapPersonaSkillStatus(PersonaSkillErrorType type)
        => MapCategoria(ToCategoria(type));

    private static int MapUsuarioStatus(UsuarioError error)
        => error.Categoria is ErrorCategoria.Unexpected && error.StatusCode is null
            ? MapUsuarioStatus(error.Type)
            : MapCategoria(error.Categoria);

    private static int MapUsuarioStatus(UsuarioErrorType type)
        => MapCategoria(ErrorCategoriaMappers.ToCategoria(type));

    private static ErrorCategoria ToCategoria(OcupacionErrorType type) => type switch
    {
        OcupacionErrorType.NotFound => ErrorCategoria.NotFound,
        OcupacionErrorType.Conflict => ErrorCategoria.Conflict,
        OcupacionErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown OcupacionErrorType value.")
    };

    private static ErrorCategoria ToCategoria(PersonaErrorType type) => type switch
    {
        PersonaErrorType.NotFound => ErrorCategoria.NotFound,
        PersonaErrorType.Conflict => ErrorCategoria.Conflict,
        PersonaErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown PersonaErrorType value.")
    };

    private static ErrorCategoria ToCategoria(PersonaSkillErrorType type) => type switch
    {
        PersonaSkillErrorType.NotFound => ErrorCategoria.NotFound,
        PersonaSkillErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown PersonaSkillErrorType value.")
    };
}