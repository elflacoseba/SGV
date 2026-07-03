using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Cliente HTTP tipado del módulo web de Habilidades.
/// Permite listar activos, obtener por id, ejecutar baja lógica, crear,
/// actualizar y consultar el catálogo de niveles de habilidad.
/// </summary>
public interface IHabilidadApiClient
{
    /// <summary>
    /// Lista todas las habilidades activas.
    /// </summary>
    Task<IReadOnlyList<HabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una habilidad activa por su identificador o <c>null</c> si no existe.
    /// </summary>
    Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la baja lógica de una habilidad y traduce la respuesta a un <see cref="HabilidadDeleteResult"/>.
    /// </summary>
    Task<HabilidadDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nueva habilidad.
    /// </summary>
    Task<HabilidadCommandResult> CreateAsync(CrearHabilidadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los campos editables (excepto <c>Codigo</c>) de una habilidad activa.
    /// </summary>
    Task<HabilidadCommandResult> UpdateAsync(Guid id, ActualizarHabilidadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el catálogo de niveles de habilidad disponible para asociaciones futuras.
    /// </summary>
    Task<IReadOnlyList<NivelHabilidadDto>> GetNivelesHabilidadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la consulta paginada y segmentada de habilidades hacia
    /// <c>GET /api/v1/skills/consulta</c>.
    /// </summary>
    Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva una habilidad eliminada lógicamente vía <c>PATCH /api/v1/skills/{id}/reactivar</c>.
    /// </summary>
    Task<HabilidadCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default);
}