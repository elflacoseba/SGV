using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Cliente HTTP tipado del módulo web de Personas.
/// Permite consultar el listado paginado y segmentado, obtener por id,
/// ejecutar baja lógica, crear, actualizar y reactivar personas. El
/// shape wire cumple el contrato de <c>SGV.Contracts.Personas</c> y la
/// rama no exitosa delega en los mappers comunes del shell web
/// (<c>ApiProblemReader</c>, <c>CommandResultMapper</c>,
/// <c>DeleteResultMapper</c>) para preservar la matriz
/// <see cref="SGV.Contracts.Comun.ErrorCategoria"/>.
/// </summary>
public interface IPersonaApiClient
{
    /// <summary>
    /// Lista todas las personas activas. Wrapper cómodo para el typeahead
    /// (PR #3) que consume <c>GET /api/v1/personas</c> sin paginar.
    /// </summary>
    Task<IReadOnlyList<PersonaDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una persona activa por su identificador o <c>null</c> si
    /// no existe o ya no está disponible. <c>404</c> se traduce a
    /// <c>null</c> sin propagar excepción (equivale al patrón
    /// recuperable de detalles).
    /// </summary>
    Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la baja lógica de una persona vía
    /// <c>DELETE /api/v1/personas/{id}</c> y traduce la respuesta a un
    /// <see cref="PersonaDeleteResult"/>. <c>204</c> es éxito;
    /// <c>404</c> se traduce a fallo con <see cref="SGV.Contracts.Comun.ErrorCategoria.NotFound"/>;
    /// cualquier otra respuesta fallida se traduce por el mapper común.
    /// </summary>
    Task<PersonaDeleteResult> DesactivarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias semánticamente equivalente a <see cref="DesactivarAsync"/>.
    /// Se conserva para alinear la nomenclatura del helper con el resto
    /// de los módulos web (Cargos expone <c>DeleteAsync</c>; lo
    /// renombramos en este módulo a <c>Desactivar</c> porque el backend
    /// implementa soft-delete) y para reservar <c>Delete*</c> para el
    /// futuro borrado físico si llegara a existir.
    /// </summary>
    Task<PersonaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => DesactivarAsync(id, cancellationToken);

    /// <summary>
    /// Crea una nueva persona vía <c>POST /api/v1/personas</c>. Devuelve
    /// éxito con el DTO persistido o un fallo tipado
    /// (<see cref="PersonaErrorType.Validation"/> con
    /// <c>FieldErrors</c>, <see cref="PersonaErrorType.Conflict"/> si
    /// hay colisión de unicidad, etc.).
    /// </summary>
    Task<PersonaCommandResult> CreateAsync(CrearPersonaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los campos editables de una persona activa vía
    /// <c>PUT /api/v1/personas/{id}</c>. Devuelve éxito con el DTO
    /// refrescado o un fallo tipado con la misma forma que
    /// <see cref="CreateAsync"/>.
    /// </summary>
    Task<PersonaCommandResult> UpdateAsync(Guid id, ActualizarPersonaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la consulta paginada y segmentada vía
    /// <c>GET /api/v1/personas/consulta</c>. <c>query.Segmento</c> se
    /// serializa como query string <c>status=activas|eliminadas</c>;
    /// cualquier valor distinto de <c>Eliminadas</c> se omite para que
    /// la API caiga a <c>activas</c> por defecto.
    /// </summary>
    Task<PersonaListadoDto> QueryAsync(PersonaListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva una persona eliminada lógicamente vía
    /// <c>PATCH /api/v1/personas/{id}/reactivar</c> y traduce la
    /// respuesta a un <see cref="PersonaCommandResult"/>.
    /// </summary>
    Task<PersonaCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side typeahead search (D-PE-03). Consume
    /// <c>GET /api/v1/personas/buscar?q={term}&amp;take={n}&amp;soloSinUsuario={bool}</c>.
    /// Devuelve hasta <paramref name="take"/> personas activas que matchean
    /// el término (case-insensitive substring sobre
    /// <c>Legajo|Nombres|Apellidos|Email|NumeroDocumento</c>), ordenadas por
    /// <c>Apellidos, Nombres</c>.
    /// <para>
    /// Reemplaza al flujo histórico del typeahead web que cargaba
    /// <c>GET /api/v1/personas</c> sin paginar (≈100 KB para 500 personas).
    /// El partial <c>_PersonaTypeahead.cshtml</c> consume este método
    /// disparando fetch en cada keystroke con debounce.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PersonaDto>> BuscarAsync(
        string? search,
        int take = 50,
        bool? soloSinUsuario = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el catálogo de tipos de documento (issue #147)
    /// disponibles para asociar al <c>NumeroDocumento</c> de una persona.
    /// Consume <c>GET /api/v1/tipos-documento</c>. Se usa en los formularios
    /// Create/Edit para popular el <c>&lt;select&gt;</c>; el fake de tests
    /// modela la misma semántica sin emitir HTTP. Espejo de
    /// <c>ICargoApiClient.GetNivelesAsync</c>.
    /// </summary>
    Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las habilidades asociadas a una persona vía
    /// <c>GET /api/v1/personas/{personaId}/skills</c>. Devuelve una lista
    /// vacía cuando el endpoint responde <c>404 Not Found</c> para que la
    /// grilla editable pueda mostrar un estado vacío sin tratar la falta
    /// de la persona como un error fatal. Cualquier otro fallo de
    /// transporte se propaga al llamador para que la página muestre un
    /// error recuperable. Subrecurso del change
    /// <c>implementa-persona-habilidades</c> (Slice 2, REQ-WEB-04).
    /// </summary>
    Task<IReadOnlyList<PersonaSkillDetailDto>> GetSkillsAsync(Guid personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna o actualiza una habilidad en una persona vía
    /// <c>PUT /api/v1/personas/{personaId}/skills/{skillId}</c> con el
    /// payload <c>{ "nivelId": "&lt;guid&gt;" }</c>. Devuelve éxito con el
    /// DTO persistido o un fallo tipado
    /// (<see cref="PersonaSkillErrorType.Validation"/> con
    /// <c>FieldErrors</c> cuando el backend emite
    /// <c>ValidationProblemDetails</c>,
    /// <see cref="PersonaSkillErrorType.NotFound"/> cuando la persona o
    /// la habilidad referenciada no existen). El mapeo no exitoso delega
    /// en <see cref="SGV.Web.Integration.Common.CommandResultMapper"/>
    /// preservando la taxonomía <see cref="SGV.Contracts.Comun.ErrorCategoria"/>
    /// que consume el PageModel de Slice 3a. Subrecurso del change
    /// <c>implementa-persona-habilidades</c> (Slice 2, REQ-WEB-04).
    /// </summary>
    Task<PersonaSkillCommandResult> UpsertSkillAsync(Guid personaId, Guid skillId, AsignarPersonaSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita una habilidad de una persona vía
    /// <c>DELETE /api/v1/personas/{personaId}/skills/{skillId}</c>.
    /// Devuelve un <see cref="PersonaSkillDeleteResult"/> con la respuesta
    /// traducida: éxito con <c>204 No Content</c>, o un fallo con el
    /// status code, el <c>Categoria</c> y el <c>ProblemDetails</c>
    /// recibido para que la página muestre un mensaje legible sin filtrar
    /// stack traces. El mapeo no exitoso delega en
    /// <see cref="SGV.Web.Integration.Common.DeleteResultMapper"/>. Subrecurso
    /// del change <c>implementa-persona-habilidades</c> (Slice 2, REQ-WEB-04).
    /// </summary>
    Task<PersonaSkillDeleteResult> DeleteSkillAsync(Guid personaId, Guid skillId, CancellationToken cancellationToken = default);
}
