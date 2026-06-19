using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre puestos.
/// </summary>
[ApiController]
[Route("api/v1/puestos")]
[Produces("application/json")]
public class PuestosController : ControllerBase
{
    private readonly IPuestoServicioConsulta _servicio;
    private readonly IPuestoServicioComandos _comandos;

    public PuestosController(
        IPuestoServicioConsulta servicio,
        IPuestoServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Obtiene todos los puestos activos.
    /// </summary>
    /// <response code="200">Lista de puestos devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PuestoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PuestoDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un puesto por su identificador único.
    /// </summary>
    /// <response code="200">Puesto encontrado.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PuestoDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo puesto.
    /// </summary>
    /// <response code="201">Puesto creado exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="409">Conflicto — ya existe un puesto activo con el mismo código.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PuestoDto>> Create(
        CrearPuestoRequest request,
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
    /// Actualiza los campos editables de un puesto existente.
    /// </summary>
    /// <response code="200">Puesto actualizado correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PuestoDto>> Update(
        Guid id,
        ActualizarPuestoRequest request,
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
    /// Elimina (soft-delete) un puesto por su identificador.
    /// </summary>
    /// <response code="204">Puesto eliminado correctamente.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.DesactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Reactiva un puesto previamente eliminado (soft-delete).
    /// </summary>
    /// <response code="200">Puesto reactivado correctamente.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    /// <response code="409">Conflicto — ya existe un puesto activo con el mismo código.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PuestoDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(PuestoError error)
    {
        var statusCode = error.Type switch
        {
            PuestoErrorType.NotFound => StatusCodes.Status404NotFound,
            PuestoErrorType.Conflict => StatusCodes.Status409Conflict,
            PuestoErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private ActionResult ToValidationProblemResult(PuestoError error, PuestoCommandResult result)
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
