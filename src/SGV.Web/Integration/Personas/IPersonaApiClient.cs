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
    /// Devuelve el catálogo de tipos de documento (issue #147)
    /// disponibles para asociar al <c>NumeroDocumento</c> de una persona.
    /// Consume <c>GET /api/v1/tipos-documento</c>. Se usa en los formularios
    /// Create/Edit para popular el <c>&lt;select&gt;</c>; el fake de tests
    /// modela la misma semántica sin emitir HTTP. Espejo de
    /// <c>ICargoApiClient.GetNivelesAsync</c>.
    /// </summary>
    Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default);
}
