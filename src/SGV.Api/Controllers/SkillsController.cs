using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones sobre habilidades.
/// </summary>
[ApiController]
[Route("api/v1/skills")]
[Produces("application/json")]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly IHabilidadServicioConsulta _servicio;
    private readonly IHabilidadServicioComandos _comandos;
    private readonly ISkillCargoServicioConsulta _skillCargoServicio;

    public SkillsController(
        IHabilidadServicioConsulta servicio,
        IHabilidadServicioComandos comandos,
        ISkillCargoServicioConsulta skillCargoServicio)
    {
        _servicio = servicio;
        _comandos = comandos;
        _skillCargoServicio = skillCargoServicio;
    }

    /// <summary>
    /// Obtiene todas las habilidades activas.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de habilidades activas.</returns>
    /// <response code="200">Lista de habilidades devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HabilidadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// Consulta paginada y filtrada de habilidades activas o eliminadas. El
    /// parámetro <c>status</c> acepta <c>activas</c> (por defecto, también
    /// usado cuando el valor es desconocido o se omite) o <c>eliminadas</c>.
    /// No mezcla ambos conjuntos en una misma respuesta.
    /// </summary>
    /// <param name="page">Número de página (1-based). Si <c>page &lt; 1</c> se normaliza a <c>1</c> en el controller.</param>
    /// <param name="pageSize">Tamaño de página. Si <c>pageSize &lt; 1</c> se normaliza a <c>20</c> (defecto). Si <c>pageSize &gt; 100</c> se limita a <c>100</c>.</param>
    /// <param name="search">Búsqueda por código, nombre, categoría o descripción.</param>
    /// <param name="sort">Expresión de orden server-side. Valores soportados: <c>codigo_asc</c>, <c>codigo_desc</c>, <c>nombre_asc</c>, <c>nombre_desc</c>, <c>categoria_asc</c>, <c>categoria_desc</c>. Cualquier otro valor cae a <c>codigo_asc</c> en el repositorio.</param>
    /// <param name="status">Filtro de estado: <c>activas</c> (por defecto) o <c>eliminadas</c>.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Resultado paginado de habilidades.</returns>
    /// <response code="200">Resultado paginado devuelto correctamente con el mismo contrato <c>HabilidadDto</c> para vistas activas o eliminadas; no mezcla ambos conjuntos en una misma respuesta.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet("consulta")]
    [ProducesResponseType(typeof(PagedResult<HabilidadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<HabilidadDto>>> GetConsulta(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        // CRITICAL-01: la normalización de page/pageSize/status vive en el
        // controller para no contaminar el record de dominio. Mantiene el
        // record HabilidadListQuery plano (POJO-like) y fija el contrato
        // HTTP documentado en el proposal/design/tasks.
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(100, pageSize);

        var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? HabilidadSegmentoListado.Eliminadas
            : HabilidadSegmentoListado.Activas;

        var query = new HabilidadListQuery(normalizedPage, normalizedPageSize, search, sort, segmento);
        var result = await _servicio.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista paginada y filtrada de cargos asociados a una habilidad.
    /// Subrecurso GET-only de <c>SkillsController</c>; cualquier usuario
    /// autenticado puede consumirlo. El parámetro <c>status</c> acepta
    /// <c>activas</c> (por defecto, también usado cuando el valor es
    /// desconocido o se omite) o <c>eliminadas</c>; <c>status</c> inválido
    /// NO produce 400, sino que resuelve a <c>activas</c>.
    /// </summary>
    /// <param name="skillId">Identificador único de la habilidad padre.</param>
    /// <param name="page">Número de página (1-based). Si <c>page &lt; 1</c> se normaliza a <c>1</c> en el controller.</param>
    /// <param name="pageSize">Tamaño de página. Si <c>pageSize &lt; 1</c> se normaliza a <c>20</c> (defecto). Si <c>pageSize &gt; 100</c> se limita a <c>100</c>.</param>
    /// <param name="search">Búsqueda por código o nombre del cargo.</param>
    /// <param name="sort">Expresión de orden server-side. Valores soportados: <c>codigo_asc</c>, <c>codigo_desc</c>, <c>nombre_asc</c>, <c>nombre_desc</c>. Cualquier otro valor cae a <c>codigo_asc</c> en el repositorio.</param>
    /// <param name="status">Filtro de estado: <c>activas</c> (por defecto) o <c>eliminadas</c>.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Resultado paginado de cargos asociados a la habilidad.</returns>
    /// <response code="200">Resultado paginado devuelto correctamente. Colección vacía si la habilidad existe pero no tiene cargos en el segmento.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="404">La habilidad padre no existe.</response>
    [HttpGet("{skillId:guid}/cargos")]
    [ProducesResponseType(typeof(PagedResult<SkillCargoDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<SkillCargoDetailDto>>> GetCargos(
        Guid skillId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        // PR-WU-A: la normalización de page/pageSize/status vive en el
        // controller para no contaminar el record de dominio. Mantiene el
        // record HabilidadCargosListQuery plano (POJO-like) y fija el
        // contrato HTTP documentado en el proposal/design/tasks. El 404
        // se distingue de la colección vacía mediante el chequeo previo
        // contra _servicio.GetByIdAsync (skill-cargo-query-contract Req 3).
        var habilidad = await _servicio.GetByIdAsync(skillId, cancellationToken);
        if (habilidad is null)
        {
            return NotFound();
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(100, pageSize);

        var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? HabilidadSegmentoListado.Eliminadas
            : HabilidadSegmentoListado.Activas;

        var query = new HabilidadCargosListQuery(normalizedPage, normalizedPageSize, search, sort, segmento);
        var result = await _skillCargoServicio.ListarCargosAsync(skillId, query, cancellationToken);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    /// Actualiza los campos editables de una habilidad existente, incluido el
    /// <c>Codigo</c>. La regla de unicidad activa del código es la misma que
    /// aplica el alta y se traduce a <c>409 Conflict</c> cuando colisiona con
    /// otra habilidad activa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking change contractual:</b> el campo <c>Codigo</c> es ahora
    /// OBLIGATORIO en el cuerpo del PUT. Consumidores que en versiones
    /// anteriores omitían <c>Codigo</c> (o no lo proveían) deben actualizar
    /// el contrato para incluirlo en cada request; de lo contrario la
    /// validación FluentValidation devuelve <c>400 Bad Request</c> con
    /// <c>ValidationProblemDetails</c> sobre <c>codigo</c>.
    /// </para>
    /// <para>
    /// El backend re-valida la unicidad activa del <c>Codigo</c> contra
    /// otras habilidades y la violación del índice único
    /// <c>IX_Habilidades_ActiveCodigoUnique</c> se traduce a
    /// <c>409 Conflict</c> con <c>CodigoDuplicado</c>.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador único de la habilidad a actualizar.</param>
    /// <param name="request">Datos actualizados de la habilidad, incluyendo el nuevo <c>Codigo</c> (obligatorio).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad actualizada.</returns>
    /// <response code="200">Habilidad actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o error de validación. Típicamente cuando <c>Codigo</c> falta, está vacío o supera los 50 caracteres.</response>
    /// <response code="404">No se encontró una habilidad con el ID especificado.</response>
    /// <response code="409">Conflicto — el código ya está en uso por otra habilidad activa.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(HabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
