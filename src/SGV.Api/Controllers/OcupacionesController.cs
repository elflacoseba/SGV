using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre ocupaciones (asignaciones histórico de persona a puesto).
/// </summary>
[ApiController]
[Route("api/v1/ocupaciones")]
[Produces("application/json")]
public class OcupacionesController : ControllerBase
{
    private readonly IOcupacionServicioConsulta _servicio;
    private readonly IOcupacionServicioComandos _comandos;

    public OcupacionesController(
        IOcupacionServicioConsulta servicio,
        IOcupacionServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Obtiene todas las ocupaciones activas. Adicionalmente, si se especifica
    /// <c>includeHistory=true</c>, se incluyen ocupaciones finalizadas y eliminadas.
    /// </summary>
    /// <param name="includeHistory">Si es <c>true</c>, incluye ocupaciones finalizadas y eliminadas.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista de ocupaciones devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OcupacionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OcupacionDto>>> GetAll(
        [FromQuery] bool includeHistory = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _servicio.ListAsync(includeHistory, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una ocupación por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la ocupación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ocupación encontrada.</response>
    /// <response code="404">No se encontró una ocupación con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OcupacionDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea una nueva ocupación.
    /// </summary>
    /// <param name="request">Datos de la ocupación a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Ocupación creada exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">Persona o puesto referenciados no existen.</response>
    /// <response code="409">Conflicto — persona inactiva, puesto inactivo, puesto ya ocupado, o persona+puesto ya ocupados.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcupacionDto>> Create(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CrearAsync(request, cancellationToken);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);

        if (result.FieldErrors is { Count: > 0 })
            return ToValidationProblemResult(result.Error!, result);

        return ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Actualiza los campos editables de una ocupación activa existente.
    /// </summary>
    /// <param name="id">Identificador único de la ocupación a actualizar.</param>
    /// <param name="request">Datos actualizados de la ocupación.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ocupación actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">Ocupación, persona o puesto no encontrados.</response>
    /// <response code="409">Conflicto — ocupación finalizada/eliminada no editable, persona inactiva, puesto inactivo, o colisión de unicidad.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcupacionDto>> Update(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ActualizarAsync(id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.FieldErrors is { Count: > 0 })
            return ToValidationProblemResult(result.Error!, result);

        return ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Finaliza una ocupación activa estableciendo su fecha de fin.
    /// </summary>
    /// <param name="id">Identificador único de la ocupación a finalizar.</param>
    /// <param name="request">Fecha de fin y observaciones opcionales.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ocupación finalizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró una ocupación con el ID especificado.</response>
    /// <response code="409">Conflicto — la ocupación ya está finalizada o eliminada.</response>
    [HttpPatch("{id:guid}/finalizar")]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcupacionDto>> Finalize(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.FinalizarAsync(id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.FieldErrors is { Count: > 0 })
            return ToValidationProblemResult(result.Error!, result);

        return ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Reactiva una ocupación previamente finalizada o eliminada lógicamente.
    /// </summary>
    /// <param name="id">Identificador único de la ocupación a reactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ocupación reactivada correctamente.</response>
    /// <response code="404">No se encontró una ocupación con el ID especificado.</response>
    /// <response code="409">Conflicto — la ocupación ya está activa, o existe colisión de unicidad con otra ocupación activa.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcupacionDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Elimina lógicamente (soft-delete) una ocupación activa.
    /// </summary>
    /// <param name="id">Identificador único de la ocupación a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Ocupación eliminada correctamente.</response>
    /// <response code="404">No se encontró una ocupación con el ID especificado.</response>
    /// <response code="409">Conflicto — la ocupación ya está finalizada o eliminada.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.EliminarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(OcupacionError error)
    {
        var statusCode = error.Type switch
        {
            OcupacionErrorType.NotFound => StatusCodes.Status404NotFound,
            OcupacionErrorType.Conflict => StatusCodes.Status409Conflict,
            OcupacionErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private ActionResult ToValidationProblemResult(OcupacionError error, OcupacionCommandResult result)
    {
        var modelState = new Dictionary<string, string[]>();
        if (result.FieldErrors is not null)
        {
            foreach (var kvp in result.FieldErrors)
            {
                modelState[kvp.Key] = kvp.Value;
            }
        }

        var details = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = error.Code,
            Detail = error.Message,
            Type = "https://httpstatuses.com/400"
        };

        return BadRequest(details);
    }
}
