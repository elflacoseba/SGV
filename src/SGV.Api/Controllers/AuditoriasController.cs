using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Auditoria;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Controllers;

/// <summary>
/// Endpoints de sólo lectura para el módulo transversal de auditoría
/// (issue <c>implementa-modulo-auditorias</c>, slice S2). Acceso
/// restringido al rol <see cref="RolesSgv.Administrador"/>. La
/// escritura de auditoría queda fuera de alcance: el servicio
/// de lectura (<see cref="IAuditoriaServicioConsulta"/>) sólo
/// proyecta el contrato wire seguro (D-2: nunca expone
/// <c>OldValuesJson</c>/<c>NewValuesJson</c>).
/// </summary>
[ApiController]
[Route("api/v1/auditorias")]
[Produces("application/json")]
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class AuditoriasController : ControllerBase
{
    private readonly IAuditoriaServicioConsulta _servicio;

    public AuditoriasController(IAuditoriaServicioConsulta servicio)
    {
        ArgumentNullException.ThrowIfNull(servicio);
        _servicio = servicio;
    }

    /// <summary>
    /// Listado paginado y filtrado de auditoría. Los filtros
    /// (<c>entityName</c>, <c>operation</c>, <c>dateFrom</c>,
    /// <c>dateTo</c>, <c>userId</c>, <c>correlationId</c>) son
    /// opcionales; omitirlos significa «no filtrar por ese criterio».
    /// El orden se controla con <c>sort</c> (ver spec
    /// <c>auditoria-sort</c>): claves válidas
    /// <c>{fecha|entidad|operacion|usuario|correlacion}_{asc|desc}</c>;
    /// valor omitido o no reconocido cae a <c>fecha_desc</c> sin
    /// error. <c>pageSize</c> se clampa a <c>[1, 100]</c> en el
    /// servicio (D-3).
    /// </summary>
    /// <param name="query">Filtros + paginación + orden.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>200 OK</c> con un <see cref="PagedResult{T}"/> de
    /// <see cref="AuditoriaDto"/>; <c>400 Validation</c> con
    /// <see cref="ProblemDetails"/> cuando <c>DateFrom &gt; DateTo</c>.
    /// </returns>
    /// <response code="200">Listado devuelto correctamente.</response>
    /// <response code="400">Rango de fechas invertido u otro error de validación del query.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor está autenticado pero no tiene el rol <c>Administrador</c>.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditoriaDto>>> Get(
        [FromQuery] AuditoriaListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _servicio
                .QueryAsync(query, cancellationToken)
                .ConfigureAwait(false);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            // El servicio rechaza DateFrom>DateTo con ArgumentException;
            // aquí lo elevamos a 400 Validation con ProblemDetails para
            // mantener una única forma de error 4xx en el wire contract.
            return ApiResults.ToValidationProblemResult(
                code: "validation_error",
                detail: ex.Message,
                fieldErrors: null,
                httpContext: HttpContext);
        }
    }

    /// <summary>
    /// Detalle de un registro de auditoría por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único de la fila de auditoría.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>200 OK</c> con el <see cref="AuditoriaDetalleDto"/> enriquecido
    /// (<c>EntityId</c> + <c>OldValuesJson</c> + <c>NewValuesJson</c> +
    /// <c>UserName</c>) cuando existe; <c>404 Not Found</c> en
    /// cualquier otro caso.
    /// </returns>
    /// <response code="200">Detalle devuelto correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="403">El consumidor está autenticado pero no tiene el rol <c>Administrador</c>.</response>
    /// <response code="404">No existe un registro con ese identificador.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditoriaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditoriaDetalleDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await _servicio
            .GetDetalleDtoAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return dto is null ? NotFound() : Ok(dto);
    }
}