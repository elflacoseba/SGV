using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Comun;
using SGV.Contracts.Setup;

namespace SGV.Api.Controllers;

/// <summary>
/// Endpoints one-time del setup inicial del primer Administrador
/// (issue #195). Ambos endpoints llevan <c>[AllowAnonymous]</c> para
/// resolver el chicken-and-egg entre ausencia de usuarios y los
/// flujos protegidos por <c>[Authorize(Roles = RolesSgv.Administrador)]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seguridad.</b> El endpoint <c>POST</c> aplica
/// <see cref="EnableRateLimitingAttribute"/> con la política
/// <c>"Setup"</c> (5 req / 15 min, idéntico patrón a
/// <c>ForgotPassword</c>). El endpoint <c>GET status</c> NO tiene
/// rate limit — es una lectura <c>O(1)</c> contra PK clustered de
/// <c>AspNetUsers</c> y se cachea 30s en el shell Web (decisión
/// design §2.3 + §2.5).
/// </para>
/// <para>
/// <b>Mapeo de errores.</b> El switch sobre <see cref="SetupErrorCode"/>
/// se hace en <see cref="Crear"/>; los códigos se traducen a
/// HTTP 400 (Validación) / 409 (Conflict) / 500 (Transacción).
/// </para>
/// </remarks>
[ApiController]
[Route(SetupApiRoutes.Base)]
[Produces("application/json")]
public sealed class SetupController(ISetupServicio setupServicio) : ControllerBase
{
    /// <summary>
    /// Estado del setup: <c>requiresSetup=true</c> cuando
    /// <c>AspNetUsers</c> está vacía. Acceso anónimo (decisión §2.5).
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con <see cref="SetupStatusResponse"/>.</returns>
    [HttpGet(SetupApiRoutes.StatusRelative)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken ct)
    {
        var response = await setupServicio.ObtenerEstadoAsync(ct).ConfigureAwait(false);
        return Ok(response);
    }

    /// <summary>
    /// Crea atómicamente Persona + Usuario + rol <c>Administrador</c>.
    /// Acceso anónimo con rate limiting (issue #195 REQ-SETUP-002).
    /// </summary>
    /// <param name="request">Datos del formulario de setup.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// 200 con <see cref="SetupCommandResult"/> en éxito; 400 con
    /// <c>ValidationProblemDetails</c> en errores de validación;
    /// 409 con <c>ProblemDetails</c> cuando el setup ya está
    /// completado o hay duplicados; 429 si el rate limit se agota;
    /// 500 con <c>ProblemDetails</c> ante fallos transaccionales.
    /// </returns>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(SetupApiRoutes.SetupPolicyName)]
    [ProducesResponseType(typeof(SetupCommandResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Crear(
        [FromBody] SetupRequest request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
        }

        var result = await setupServicio.CrearAdminAsync(request, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Ok(result);
        }

        var error = result.Error!;
        var statusCode = error.StatusCode ?? MapCategoriaToStatus(error.Categoria);

        return statusCode switch
        {
            400 => new BadRequestObjectResult(BuildValidationProblem(error, result.FieldErrors)),
            409 => Conflict(BuildProblem(error, 409)),
            429 => StatusCode(StatusCodes.Status429TooManyRequests),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                BuildProblem(error, 500))
        };
    }

    private static int MapCategoriaToStatus(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.Validation => StatusCodes.Status400BadRequest,
        ErrorCategoria.Conflict => StatusCodes.Status409Conflict,
        ErrorCategoria.NotFound => StatusCodes.Status404NotFound,
        ErrorCategoria.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategoria.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategoria.Transport => StatusCodes.Status503ServiceUnavailable,
        ErrorCategoria.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };

    private static ProblemDetails BuildProblem(SetupError error, int statusCode)
        => new()
        {
            Status = statusCode,
            Title = error.Code.ToString(),
            Detail = error.Message,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

    private static ValidationProblemDetails BuildValidationProblem(
        SetupError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        var modelState = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (fieldErrors is not null)
        {
            foreach (var kvp in fieldErrors)
            {
                modelState[kvp.Key] = kvp.Value;
            }
        }

        return new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = error.Code.ToString(),
            Detail = error.Message,
            Type = $"https://httpstatuses.com/{StatusCodes.Status400BadRequest}"
        };
    }
}
