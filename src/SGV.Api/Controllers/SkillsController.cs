using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre habilidades.
/// </summary>
[ApiController]
[Route("api/v1/skills")]
[Produces("application/json")]
public class SkillsController : ControllerBase
{
    private readonly IHabilidadServicioConsulta _servicio;
    private readonly IHabilidadServicioComandos _comandos;

    public SkillsController(
        IHabilidadServicioConsulta servicio,
        IHabilidadServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Obtiene todas las habilidades activas.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de habilidades activas.</returns>
    /// <response code="200">Lista de habilidades devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HabilidadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HabilidadDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una habilidad por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la habilidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad solicitada.</returns>
    /// <response code="200">Habilidad encontrada.</response>
    /// <response code="404">No se encontró una habilidad con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HabilidadDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea una nueva habilidad.
    /// </summary>
    /// <param name="request">Datos de la habilidad a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad creada con su localización.</returns>
    /// <response code="201">Habilidad creada exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="409">Conflicto — ya existe una habilidad activa con el mismo código.</response>
    [HttpPost]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabilidadDto>> Create(
        CrearHabilidadRequest request,
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
    /// Actualiza los campos editables de una habilidad existente.
    /// </summary>
    /// <param name="id">Identificador único de la habilidad a actualizar.</param>
    /// <param name="request">Datos actualizados de la habilidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad actualizada.</returns>
    /// <response code="200">Habilidad actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró una habilidad con el ID especificado.</response>
    /// <response code="409">Conflicto — el código ya está en uso por otra habilidad.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabilidadDto>> Update(
        Guid id,
        ActualizarHabilidadRequest request,
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
    /// Desactiva (soft-delete) una habilidad por su identificador.
    /// </summary>
    /// <param name="id">Identificador único de la habilidad a desactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Habilidad desactivada correctamente.</response>
    /// <response code="404">No se encontró una habilidad con el ID especificado.</response>
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
    /// Reactiva una habilidad previamente desactivada (soft-delete).
    /// </summary>
    /// <param name="id">Identificador único de la habilidad a reactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad reactivada.</returns>
    /// <response code="200">Habilidad reactivada correctamente.</response>
    /// <response code="404">No se encontró una habilidad con el ID especificado.</response>
    /// <response code="409">Conflicto — ya existe una habilidad activa con el mismo código.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabilidadDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(HabilidadError error)
    {
        var statusCode = error.Type switch
        {
            HabilidadErrorType.NotFound => StatusCodes.Status404NotFound,
            HabilidadErrorType.Conflict => StatusCodes.Status409Conflict,
            HabilidadErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private ActionResult ToValidationProblemResult(HabilidadError error, HabilidadCommandResult result)
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
