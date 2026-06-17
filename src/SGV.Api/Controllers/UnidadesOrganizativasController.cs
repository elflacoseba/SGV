using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre unidades organizativas.
/// </summary>
[ApiController]
[Route("api/v1/unidades-organizativas")]
[Produces("application/json")]
public class UnidadesOrganizativasController : ControllerBase
{
    private readonly IUnidadOrganizativaServicioConsulta _servicio;
    private readonly IUnidadOrganizativaServicioComandos _comandos;

    public UnidadesOrganizativasController(
        IUnidadOrganizativaServicioConsulta servicio,
        IUnidadOrganizativaServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Obtiene todas las unidades organizativas activas.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de unidades organizativas activas.</returns>
    /// <response code="200">Lista de unidades organizativas devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UnidadOrganizativaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnidadOrganizativaDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una unidad organizativa por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la unidad organizativa.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Unidad organizativa solicitada.</returns>
    /// <response code="200">Unidad organizativa encontrada.</response>
    /// <response code="404">No se encontró una unidad organizativa con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UnidadOrganizativaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnidadOrganizativaDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Crea una nueva unidad organizativa.
    /// </summary>
    /// <param name="request">Datos de la unidad organizativa a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Unidad organizativa creada con su localización.</returns>
    /// <response code="201">Unidad organizativa creada exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="409">Conflicto — ya existe una unidad con el mismo código o nombre.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UnidadOrganizativaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Create(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Actualiza los campos editables de una unidad organizativa existente.
    /// </summary>
    /// <param name="id">Identificador único de la unidad organizativa a actualizar.</param>
    /// <param name="request">Datos actualizados de la unidad organizativa.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Unidad organizativa actualizada.</returns>
    /// <response code="200">Unidad organizativa actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró una unidad organizativa con el ID especificado.</response>
    /// <response code="409">Conflicto — el código o nombre ya está en uso por otra unidad.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UnidadOrganizativaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Update(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ActualizarAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Cambia la unidad padre (superior jerárquico) de una unidad organizativa.
    /// </summary>
    /// <param name="id">Identificador único de la unidad organizativa.</param>
    /// <param name="request">Contiene el ID de la nueva unidad padre (puede ser null para dejar sin padre).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Unidad organizativa con la relación jerárquica actualizada.</returns>
    /// <response code="200">Unidad padre actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="404">No se encontró la unidad organizativa o la unidad padre especificada.</response>
    /// <response code="409">Conflicto — el cambio de padre crearía un ciclo en la jerarquía.</response>
    [HttpPatch("{id:guid}/unidad-padre")]
    [ProducesResponseType(typeof(UnidadOrganizativaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> ChangeParent(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CambiarUnidadPadreAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Elimina (soft-delete) una unidad organizativa por su identificador.
    /// </summary>
    /// <param name="id">Identificador único de la unidad organizativa a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Unidad organizativa eliminada correctamente.</response>
    /// <response code="404">No se encontró una unidad organizativa con el ID especificado.</response>
    /// <response code="409">Conflicto — la unidad tiene hijos o recursos asociados que impiden la eliminación.</response>
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

    /// <summary>
    /// Reactiva una unidad organizativa previamente eliminada (soft-delete).
    /// </summary>
    /// <param name="id">Identificador único de la unidad organizativa a reactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Unidad organizativa reactivada.</returns>
    /// <response code="200">Unidad organizativa reactivada correctamente.</response>
    /// <response code="404">No se encontró una unidad organizativa con el ID especificado.</response>
    /// <response code="409">Conflicto — no se puede reactivar porque la unidad padre sigue eliminada.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [ProducesResponseType(typeof(UnidadOrganizativaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadOrganizativaDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(UnidadOrganizativaError error)
    {
        var statusCode = error.Type switch
        {
            UnidadOrganizativaErrorType.NotFound => StatusCodes.Status404NotFound,
            UnidadOrganizativaErrorType.Conflict => StatusCodes.Status409Conflict,
            UnidadOrganizativaErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }
}
