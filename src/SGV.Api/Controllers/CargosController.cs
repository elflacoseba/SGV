using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre cargos.
/// </summary>
[ApiController]
[Route("api/v1/cargos")]
[Produces("application/json")]
public class CargosController : ControllerBase
{
    private readonly ICargoServicioConsulta _servicio;
    private readonly ICargoServicioComandos _comandos;
    private readonly ICargoSkillServicio _skillServicio;

    public CargosController(
        ICargoServicioConsulta servicio,
        ICargoServicioComandos comandos,
        ICargoSkillServicio skillServicio)
    {
        _servicio = servicio;
        _comandos = comandos;
        _skillServicio = skillServicio;
    }

    /// <summary>
    /// Obtiene todos los cargos activos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de cargos activos.</returns>
    /// <response code="200">Lista de cargos devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CargoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CargoDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un cargo por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único del cargo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Cargo solicitado.</returns>
    /// <response code="200">Cargo encontrado.</response>
    /// <response code="404">No se encontró un cargo con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CargoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CargoDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo cargo.
    /// </summary>
    /// <param name="request">Datos del cargo a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Cargo creado con su localización.</returns>
    /// <response code="201">Cargo creado exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="409">Conflicto — ya existe un cargo activo con el mismo código.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CargoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CargoDto>> Create(
        CrearCargoRequest request,
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
    /// Actualiza los campos editables de un cargo existente.
    /// </summary>
    /// <param name="id">Identificador único del cargo a actualizar.</param>
    /// <param name="request">Datos actualizados del cargo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Cargo actualizado.</returns>
    /// <response code="200">Cargo actualizado correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró un cargo con el ID especificado.</response>
    /// <response code="409">Conflicto — el código ya está en uso por otro cargo.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CargoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CargoDto>> Update(
        Guid id,
        ActualizarCargoRequest request,
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
    /// Elimina (soft-delete) un cargo por su identificador.
    /// </summary>
    /// <param name="id">Identificador único del cargo a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Cargo eliminado correctamente.</response>
    /// <response code="404">No se encontró un cargo con el ID especificado.</response>
    /// <response code="409">Conflicto — el cargo tiene puestos activos asociados que impiden la eliminación.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    /// Reactiva un cargo previamente eliminado (soft-delete).
    /// </summary>
    /// <param name="id">Identificador único del cargo a reactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Cargo reactivado.</returns>
    /// <response code="200">Cargo reactivado correctamente.</response>
    /// <response code="404">No se encontró un cargo con el ID especificado.</response>
    /// <response code="409">Conflicto — ya existe un cargo activo con el mismo código.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(CargoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CargoDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    // ---- Subrecurso: habilidades del cargo ----

    /// <summary>
    /// Lista las habilidades asociadas a un cargo.
    /// </summary>
    /// <param name="cargoId">Identificador único del cargo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de habilidades asignadas al cargo.</returns>
    /// <response code="200">Lista de habilidades devuelta correctamente.</response>
    [HttpGet("{cargoId:guid}/skills")]
    [ProducesResponseType(typeof(IReadOnlyList<CargoSkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CargoSkillDto>>> GetSkills(
        Guid cargoId,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.ListAsync(cargoId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Asigna o actualiza una habilidad en un cargo.
    /// </summary>
    /// <param name="cargoId">Identificador único del cargo.</param>
    /// <param name="skillId">Identificador único de la habilidad.</param>
    /// <param name="request">Nivel de competencia requerido.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad asignada al cargo.</returns>
    /// <response code="200">Habilidad asignada o actualizada correctamente.</response>
    /// <response code="400">Nivel de habilidad inválido.</response>
    /// <response code="404">Cargo o habilidad no encontrados.</response>
    [HttpPut("{cargoId:guid}/skills/{skillId:guid}")]
    [ProducesResponseType(typeof(CargoSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CargoSkillDto>> UpsertSkill(
        Guid cargoId,
        Guid skillId,
        AsignarCargoSkillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.UpsertAsync(cargoId, skillId, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToSkillProblemResult(result.Error!);
    }

    /// <summary>
    /// Elimina físicamente una habilidad asignada a un cargo.
    /// </summary>
    /// <param name="cargoId">Identificador único del cargo.</param>
    /// <param name="skillId">Identificador único de la habilidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Habilidad eliminada correctamente.</response>
    /// <response code="404">Cargo o asignación no encontrados.</response>
    [HttpDelete("{cargoId:guid}/skills/{skillId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteSkill(
        Guid cargoId,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.DeleteAsync(cargoId, skillId, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ToSkillProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(CargoError error)
    {
        var statusCode = error.Type switch
        {
            CargoErrorType.NotFound => StatusCodes.Status404NotFound,
            CargoErrorType.Conflict => StatusCodes.Status409Conflict,
            CargoErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private ActionResult ToValidationProblemResult(CargoError error, CargoCommandResult result)
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

    private ActionResult ToSkillProblemResult(CargoSkillError error)
    {
        var statusCode = error.Type switch
        {
            CargoSkillErrorType.NotFound => StatusCodes.Status404NotFound,
            CargoSkillErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }
}
