using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre ocupaciones (asignaciones histórico de persona a puesto).
/// </summary>
[ApiController]
[Route("api/v1/ocupaciones")]
[Produces("application/json")]
[Authorize]
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
    /// Los resultados se devuelven paginados.
    /// </summary>
    /// <param name="includeHistory">Si es <c>true</c>, incluye ocupaciones finalizadas y eliminadas.</param>
    /// <param name="page">Número de página (comienza en 1).</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista paginada de ocupaciones devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OcupacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<OcupacionDto>>> GetAll(
        [FromQuery] bool includeHistory = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _servicio.ListAsync(includeHistory, page, pageSize, cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(OcupacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OcupacionDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.EliminarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }
}
