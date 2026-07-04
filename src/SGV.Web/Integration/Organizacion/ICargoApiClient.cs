using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP tipado del módulo web de Cargos.
/// Permite listar activos, obtener por id, ejecutar baja lógica, crear
/// nuevos cargos y consultar el catálogo de niveles de cargo.
/// </summary>
public interface ICargoApiClient
{
    /// <summary>
    /// Lista todos los cargos activos.
    /// </summary>
    Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un cargo activo por su identificador o <c>null</c> si no existe o ya no está disponible.
    /// </summary>
    Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la baja lógica de un cargo y traduce la respuesta a un <see cref="CargoDeleteResult"/>.
    /// </summary>
    Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo cargo. Devuelve éxito con el DTO persistido o un fallo tipado
    /// (<see cref="CargoErrorType.Validation"/> con <c>FieldErrors</c>,
    /// <see cref="CargoErrorType.Conflict"/> si el código está duplicado contra un
    /// cargo activo, etc.).
    /// </summary>
    Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los campos editables (incluido <c>Codigo</c>) de un cargo activo.
    /// Devuelve éxito con el DTO refrescado o un fallo tipado con la misma forma que
    /// <see cref="CreateAsync"/>: <see cref="CargoErrorType.Validation"/> con
    /// <c>FieldErrors</c> en el caso de un 400, <see cref="CargoErrorType.Conflict"/>
    /// si el nuevo <c>Codigo</c> colisiona con otro cargo activo, etc.
    /// </summary>
    Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el catálogo de niveles de cargo disponible para asociar a un cargo.
    /// </summary>
    Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la consulta paginada y segmentada de cargos hacia
    /// <c>GET /api/v1/cargos/consulta</c>. <c>query.Status</c> se serializa
    /// como query string <c>status=activas|eliminadas</c>; cualquier valor
    /// distinto de <c>eliminadas</c> se omite para que la API caiga a
    /// <c>activas</c> por defecto.
    /// </summary>
    Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva un cargo eliminado lógicamente vía <c>PATCH /api/v1/cargos/{id}/reactivar</c>
    /// y traduce la respuesta a un <see cref="CargoCommandResult"/>.
    /// </summary>
    Task<CargoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las habilidades asociadas a un cargo vía
    /// <c>GET /api/v1/cargos/{cargoId}/skills</c>. Devuelve una lista vacía
    /// cuando el endpoint responde <c>404 Not Found</c> para que la grilla
    /// editable pueda mostrar un estado vacío sin tratar la falta del cargo
    /// como un error fatal. Cualquier otro fallo de transporte se propaga al
    /// llamador para que la página muestre un error recuperable.
    /// </summary>
    Task<IReadOnlyList<CargoSkillDetailDto>> GetSkillsAsync(Guid cargoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna o actualiza una habilidad en un cargo vía
    /// <c>PUT /api/v1/cargos/{cargoId}/skills/{skillId}</c>. Devuelve éxito con
    /// el DTO persistido o un fallo tipado
    /// (<see cref="CargoSkillErrorType.Validation"/> con <c>FieldErrors</c> cuando
    /// el backend emite <c>ValidationProblemDetails</c>,
    /// <see cref="CargoSkillErrorType.Validation"/> sin <c>FieldErrors</c> para
    /// <c>ProblemDetails</c> planos, <see cref="CargoSkillErrorType.NotFound"/>
    /// cuando el cargo o la habilidad no existen, etc.).
    /// </summary>
    Task<CargoSkillCommandResult> UpsertSkillAsync(Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita una habilidad de un cargo vía
    /// <c>DELETE /api/v1/cargos/{cargoId}/skills/{skillId}</c>. Devuelve un
    /// <see cref="CargoSkillDeleteResult"/> con la respuesta traducida: éxito
    /// con <c>204 No Content</c>, o un fallo con el status code y el
    /// <c>ProblemDetails</c> recibido para que la página muestre un mensaje
    /// legible sin filtrar stack traces.
    /// </summary>
    Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default);
}
