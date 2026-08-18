using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre puestos.
/// </summary>
[ApiController]
[Route("api/v1/puestos")]
[Produces("application/json")]
[Authorize]
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
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PuestoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PuestoDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene los puestos disponibles (sin Ocupación vigente ni Vacante abierta).
    /// </summary>
    /// <response code="200">Lista de puestos disponibles devuelta correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(IReadOnlyList<PuestoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PuestoDto>>> GetDisponibles(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListarDisponiblesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un puesto por su identificador único.
    /// </summary>
    /// <response code="200">Puesto encontrado.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor no tiene rol <c>Administrador</c>.</response>
    /// <response code="409">Conflicto — ya existe un puesto activo con el mismo código.</response>
    [HttpPost]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PuestoDto>> Create(
        CrearPuestoRequest request,
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
    /// Actualiza los campos editables de un puesto existente.
    /// </summary>
    /// <remarks><c>409 Conflict</c> no aplica aquí porque <c>Codigo</c> es inmutable en un puesto existente. La unicidad activa sólo se valida en <c>Crear</c> y <c>Reactivar</c>.</remarks>
    /// <response code="200">Puesto actualizado correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor no tiene rol <c>Administrador</c>.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    /// <summary>
    /// Elimina (soft-delete) un puesto por su identificador.
    /// </summary>
    /// <response code="204">Puesto eliminado correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor no tiene rol <c>Administrador</c>.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    /// <response code="409">Conflicto — el puesto tiene ocupaciones vigentes y no puede darse de baja.</response>
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
        var result = await _comandos.DesactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    /// <summary>
    /// Consulta paginada y filtrada de puestos activos o eliminados. El parámetro
    /// <c>status</c> selecciona el segmento (<c>eliminadas</c> selecciona
    /// eliminados; cualquier otro valor, incluido ausente, selecciona activos).
    /// </summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="search">Substring filter sobre <c>Codigo</c>, <c>Nombre</c> y opcionalmente <c>Descripcion</c>.</param>
    /// <param name="sort">Sort expression (e.g. <c>nombre_asc</c>).</param>
    /// <param name="status">Segmento: <c>eliminadas</c> para soft-deleted; resto = activas.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado paginado de puestos (<c>PagedResult&lt;PuestoDto&gt;</c>) para el segmento y página solicitada.</returns>
    /// <response code="200">Resultado paginado devuelto correctamente con el mismo contrato <c>PuestoDto</c> para vistas activas o eliminadas; no mezcla ambos conjuntos en una misma respuesta.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet("consulta")]
    [ProducesResponseType(typeof(PagedResult<PuestoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<PuestoDto>>> GetConsulta(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        // Normalización de page/pageSize en el controller (espejo de
        // SkillsController.GetConsulta, spec CRITICAL-01): page<1 cae a 1,
        // pageSize<1 cae al default 20 y pageSize>100 se capa a 100 para
        // proteger la query del repo contra DOS accidentales.
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(100, pageSize);

        var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? PuestoSegmentoListado.Eliminadas
            : PuestoSegmentoListado.Activas;

        var query = new PuestoListQuery(normalizedPage, normalizedPageSize, search, sort, segmento);
        var result = await _servicio.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reactiva un puesto previamente eliminado (soft-delete).
    /// </summary>
    /// <response code="200">Puesto reactivado correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor no tiene rol <c>Administrador</c>.</response>
    /// <response code="404">No se encontró un puesto con el ID especificado.</response>
    /// <response code="409">Conflicto — ya existe un puesto activo con el mismo código.</response>
    [HttpPatch("{id:guid}/reactivar")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PuestoDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }
}
