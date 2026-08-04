using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Controllers;

/// <summary>
/// CRUD y operaciones administrativas sobre personas.
/// </summary>
[ApiController]
[Route("api/v1/personas")]
[Produces("application/json")]
[Authorize]
public class PersonasController : ControllerBase
{
    private readonly IPersonaServicioConsulta _servicio;
    private readonly IPersonaServicioComandos _comandos;
    private readonly IPersonaSkillServicio _skillServicio;

    public PersonasController(
        IPersonaServicioConsulta servicio,
        IPersonaServicioComandos comandos,
        IPersonaSkillServicio skillServicio)
    {
        _servicio = servicio;
        _comandos = comandos;
        _skillServicio = skillServicio;
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
    /// Consulta paginada y filtrada de personas activas o eliminadas. El parámetro
    /// <c>status</c> acepta <c>activas</c> (por defecto, también usado cuando el
    /// valor es desconocido o se omite) o <c>eliminadas</c>. No mezcla ambos
    /// conjuntos en una misma respuesta. El parámetro <c>soloSinUsuario</c>
    /// restringe el segmento <c>activas</c> a personas sin
    /// <c>AspNetUsers.PersonaId</c> asociado (REQ-PM-01); ausente, <c>false</c>
    /// o <c>null</c> preserva el comportamiento vigente. Cualquier usuario
    /// autenticado puede invocar este endpoint; las mutaciones siguen
    /// requiriendo <c>Administrador</c> (ver <see cref="Create"/>,
    /// <see cref="Update"/>, <see cref="Delete"/>, <see cref="Reactivate"/>).
    /// </summary>
    /// <param name="page">Número de página (default: 1).</param>
    /// <param name="pageSize">Tamaño de página (default: 20, máximo 100).</param>
    /// <param name="search">Búsqueda substring case-insensitive sobre <c>Legajo|Nombres|Apellidos|Email|NumeroDocumento</c>.</param>
    /// <param name="sort">Expresión de orden server-side (e.g. <c>apellidos_desc</c>). Valores soportados: <c>legajo_asc/desc</c>, <c>apellidos_asc/desc</c>, <c>nombres_asc/desc</c>, <c>email_asc/desc</c>. Cualquier otro valor cae a <c>apellidos_asc</c>.</param>
    /// <param name="status">Filtro de estado: <c>activas</c> (por defecto) o <c>eliminadas</c>.</param>
    /// <param name="soloSinUsuario">Cuando es <c>true</c>, restringe el segmento activo a personas sin usuario activo asociado. Cualquier otro valor es ignorado.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Resultado paginado de personas usando el contrato <c>PersonaListadoDto</c>.</returns>
    /// <response code="200">Resultado paginado devuelto correctamente con el mismo contrato <c>PersonaDto</c> para vistas activas o eliminadas; no mezcla ambos conjuntos en una misma respuesta.</response>
    /// <response code="400">Tamaño de página fuera de rango (1..100).</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet("consulta")]
    [ProducesResponseType(typeof(PersonaListadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PersonaListadoDto>> GetConsulta(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? soloSinUsuario = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "ParametrosInvalidos",
                Detail = "page debe ser >= 1 y pageSize debe estar entre 1 y 100.",
                Type = "https://httpstatuses.com/400"
            });
        }

        var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? PersonaSegmentoListado.Eliminadas
            : PersonaSegmentoListado.Activas;

        var query = new PersonaListQuery(
            page, pageSize, search, sort, segmento, soloSinUsuario);
        var result = await _servicio.ListarAsync(query, cancellationToken);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Create(
        CrearPersonaRequest request,
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    /// <summary>
    /// Desactiva (soft-delete) una persona por su identificador.
    /// </summary>
    /// <param name="id">Identificador único de la persona a desactivar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Persona desactivada correctamente.</response>
    /// <response code="404">No se encontró una persona con el ID especificado.</response>
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
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
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
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PersonaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonaDto>> Reactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    // ---- Subrecurso: habilidades de la persona ----

    /// <summary>
    /// Lista las habilidades asociadas a una persona.
    /// </summary>
    /// <param name="personaId">Identificador único de la persona.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de habilidades asignadas a la persona.</returns>
    /// <response code="200">Lista de habilidades devuelta correctamente.</response>
    [HttpGet("{personaId:guid}/skills")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonaSkillDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonaSkillDetailDto>>> GetSkills(
        Guid personaId,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.ListAsync(personaId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Asigna o actualiza una habilidad en una persona.
    /// </summary>
    /// <param name="personaId">Identificador único de la persona.</param>
    /// <param name="skillId">Identificador único de la habilidad.</param>
    /// <param name="request">Nivel de dominio/proficiencia.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Habilidad asignada a la persona.</returns>
    /// <response code="200">Habilidad asignada o actualizada correctamente.</response>
    /// <response code="400">Nivel de habilidad inválido.</response>
    /// <response code="404">Persona o habilidad no encontradas.</response>
    [HttpPut("{personaId:guid}/skills/{skillId:guid}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(PersonaSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonaSkillDto>> UpsertSkill(
        Guid personaId,
        Guid skillId,
        AsignarPersonaSkillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.UpsertAsync(personaId, skillId, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        return ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    /// <summary>
    /// Elimina físicamente una habilidad asignada a una persona.
    /// </summary>
    /// <param name="personaId">Identificador único de la persona.</param>
    /// <param name="skillId">Identificador único de la habilidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="204">Habilidad eliminada correctamente.</response>
    /// <response code="404">Persona o asignación no encontradas.</response>
    [HttpDelete("{personaId:guid}/skills/{skillId:guid}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteSkill(
        Guid personaId,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var result = await _skillServicio.DeleteAsync(personaId, skillId, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }
}
