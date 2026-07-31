using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Vacantes.Comandos;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Vacantes;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Contracts.Vacantes.Enums;

namespace SGV.Api.Controllers;

/// <summary>
/// HTTP endpoints for Vacante management. Reads
/// (<c>GET /api/v1/vacantes</c> + <c>GET /api/v1/vacantes/{id}</c>) only
/// require authentication; mutations
/// (<c>POST</c> + <c>PATCH /{id}/estado</c>) are gated by
/// <see cref="RolesSgv.RolesSgvMutacion"/> per PB-1. The
/// <c>status</c> query string accepts <c>abiertas | cerradas | todas</c>
/// and normalises anything else (including null/empty) to
/// <see cref="VacanteSegmentoListado.Abiertas"/> per PB-5.
/// </summary>
[ApiController]
[Route(VacanteApiRoutes.Base)]
[Produces("application/json")]
[Authorize]
public class VacantesController : ControllerBase
{
    /// <summary>
    /// Tope máximo de <c>pageSize</c> aceptado por el listado. Protege
    /// contra requests que pidan miles de filas en una sola página y
    /// mantiene latencia predecible.
    /// </summary>
    private const int MaxPageSize = 100;

    private readonly IVacanteServicioConsulta _servicio;
    private readonly IVacanteServicioComandos _comandos;

    public VacantesController(
        IVacanteServicioConsulta servicio,
        IVacanteServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
    }

    /// <summary>
    /// Paginated, segmented list of vacantes.
    /// </summary>
    /// <remarks>
    /// PB-5: el parámetro <c>status</c> acepta <c>abiertas</c> (default,
    /// también usado cuando el valor es desconocido o se omite),
    /// <c>cerradas</c> o <c>todas</c>. No mezcla ambos conjuntos en una
    /// misma respuesta (segmento se evalúa como join a
    /// <c>EstadoVacante.EsTerminal</c> en repository, <c>design.md</c>
    /// §D-2).
    /// </remarks>
    /// <param name="status">Filtro de segmento: <c>abiertas</c> (default),
    /// <c>cerradas</c> o <c>todas</c>.</param>
    /// <param name="page">Número de página (default: 1, mínimo: 1).
    /// Valores ≤ 0 se normalizan a 1.</param>
    /// <param name="pageSize">Tamaño de página (default: 20, rango: 1..100).
    /// Valores fuera de rango se clampan. Esto evita respuestas
    /// impredecibles si el cliente envía <c>pageSize=-1</c> o
    /// <c>pageSize=10000</c>.</param>
    /// <param name="search">Búsqueda por nombre de puesto, motivo u observaciones.</param>
    /// <param name="sort">Expresión de orden server-side (e.g.
    /// <c>fechaapertura_desc</c>, <c>puesto_asc</c>). Cualquier otro valor
    /// cae a <c>fechaapertura_desc</c>.</param>
    /// <param name="puestoId">Filtro opcional por puesto.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado paginado devuelto correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VacanteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<VacanteDto>>> Get(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] Guid? puestoId = null,
        CancellationToken cancellationToken = default)
    {
        var segmento = NormalizeSegmento(status);
        var pageValida = Math.Max(1, page);
        var pageSizeValido = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = new VacanteListQuery(pageValida, pageSizeValido, search, sort, segmento, puestoId);
        var result = await _servicio.ListarAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a vacante by id including its
    /// <c>HistorialEstadoVacante</c> in chronological order.
    /// </summary>
    /// <param name="id">Identificador único de la vacante.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Vacante encontrada.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="404">No se encontró una vacante con el ID especificado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VacanteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VacanteDetailDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Opens a new vacante. PB-1: requires <see cref="RolesSgv.RolesSgvMutacion"/>.
    /// PB-2: creation only happens from this endpoint, NOT from
    /// <c>Puestos/Details</c>.
    /// </summary>
    /// <param name="request">Datos de la vacante a crear.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Vacante creada exitosamente.</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El rol del consumidor no permite la mutación.</response>
    /// <response code="404">Estado de vacante destino no existe.</response>
    /// <response code="409">Ya existe una vacante abierta para el puesto
    /// (<c>PuestoConVacanteAbierta</c>).</response>
    [HttpPost]
    [Authorize(Roles = RolesSgv.RolesSgvMutacion)]
    [ProducesResponseType(typeof(VacanteDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VacanteDetailDto>> Create(
        CrearVacanteRequest request,
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
    /// Transitions a vacante to a new state and persists the matching
    /// <c>HistorialEstadoVacante</c> row in the same EF transaction.
    /// PB-3: <c>Motivo</c> opcional. PB-1: requires
    /// <see cref="RolesSgv.RolesSgvMutacion"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Observaciones:</b> Pasar <c>null</c>, string vacío o solo
    /// espacios en <c>request.Observaciones</c> <b>limpia</b> el campo
    /// <c>Observaciones</c> de la vacante (no las deja intactas). El
    /// dominio normaliza estos valores a <c>null</c> vía
    /// <c>ValidacionesDominio.Opcional</c>. Para mantener el valor
    /// actual, omitir el campo no es suficiente (la deserialización lo
    /// setea a <c>null</c>); la API siempre escribe lo que reciba.</para>
    /// <para><b>Atomicidad:</b> la mutación del estado + el insert del
    /// historial + la actualización de <c>Observaciones</c> se commitean
    /// en una sola transacción EF (<c>design.md</c> §D-5).</para>
    /// </remarks>
    /// <param name="id">Identificador único de la vacante.</param>
    /// <param name="request">Estado destino + motivo opcional + observaciones opcionales.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Vacante transicionada correctamente (incluye el nuevo historial).</response>
    /// <response code="400">Datos inválidos o error de validación.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El rol del consumidor no permite la mutación.</response>
    /// <response code="404">Vacante o estado destino inexistente.</response>
    /// <response code="409">La vacante ya está en un estado terminal
    /// (<c>EstadoTerminalInmutable</c>) o la transición viola una
    /// constraint de BD.</response>
    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = RolesSgv.RolesSgvMutacion)]
    [ProducesResponseType(typeof(VacanteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VacanteDetailDto>> CambiarEstado(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CambiarEstadoAsync(id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.FieldErrors is { Count: > 0 })
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    private static VacanteSegmentoListado NormalizeSegmento(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return VacanteSegmentoListado.Abiertas;
        }

        if (string.Equals(status, VacanteApiRoutes.StatusCerradas, StringComparison.OrdinalIgnoreCase))
        {
            return VacanteSegmentoListado.Cerradas;
        }

        if (string.Equals(status, VacanteApiRoutes.StatusTodas, StringComparison.OrdinalIgnoreCase))
        {
            return VacanteSegmentoListado.Todas;
        }

        // PB-5: cualquier valor desconocido cae a Abiertas (sin mezclar segmentos).
        return VacanteSegmentoListado.Abiertas;
    }
}