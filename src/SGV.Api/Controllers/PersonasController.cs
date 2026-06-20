using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones administrativas sobre personas.
/// </summary>
[ApiController]
[Route("api/v1/personas")]
[Produces("application/json")]
public class PersonasController : ControllerBase
{
    private readonly IPersonaServicioConsulta _servicio;
    private readonly IPersonaServicioComandos _comandos;

    public PersonasController(
        IPersonaServicioConsulta servicio,
        IPersonaServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Obtiene todas las personas activas.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de personas activas.</returns>
    /// <response code="200">Lista de personas devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PersonaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonaDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una persona por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la persona.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Persona solicitada.</returns>
    /// <response code="200">Persona encontrada.</response>
    /// <response code="404">No se encontró una persona con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonaDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea una nueva persona.
    /// </summary>
    /// <param name="request">Datos de la persona a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Persona creada con su localización.</returns>
    /// <response code="201">Persona creada exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="409">Conflicto — ya existe una persona activa con el mismo legajo, email o documento.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Create(
        CrearPersonaRequest request,
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
    /// Actualiza los campos editables de una persona existente.
    /// </summary>
    /// <param name="id">Identificador único de la persona a actualizar.</param>
    /// <param name="request">Datos actualizados de la persona.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Persona actualizada.</returns>
    /// <response code="200">Persona actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró una persona con el ID especificado.</response>
    /// <response code="409">Conflicto — el legajo, email o documento ya está en uso por otra persona activa.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Update(
        Guid id,
        ActualizarPersonaRequest request,
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
    /// Desactiva (soft-delete) una persona por su identificador.
    /// </summary>
    /// <param name="id">Identificador único de la persona a desactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Persona desactivada correctamente.</response>
    /// <response code="404">No se encontró una persona con el ID especificado.</response>
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
    /// Reactiva una persona previamente desactivada (soft-delete).
    /// </summary>
    /// <param name="id">Identificador único de la persona a reactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Persona reactivada.</returns>
    /// <response code="200">Persona reactivada correctamente.</response>
    /// <response code="404">No se encontró una persona con el ID especificado.</response>
    /// <response code="409">Conflicto — ya existe una persona activa con el mismo legajo, email o documento.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(PersonaError error)
    {
        var statusCode = error.Type switch
        {
            PersonaErrorType.NotFound => StatusCodes.Status404NotFound,
            PersonaErrorType.Conflict => StatusCodes.Status409Conflict,
            PersonaErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }

    private ActionResult ToValidationProblemResult(PersonaError error, PersonaCommandResult result)
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
