using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones.Consultas;

/// <summary>
/// Repository contract for Ocupacion read and write operations.
/// </summary>
public interface IOcupacionRepository : IReadOnlyRepository<Ocupacion>
{
    /// <summary>
    /// Adds a new occupation.
    /// </summary>
    Task AddAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted, non-finalized occupation for update.
    /// Includes Persona and Puesto navigation properties.
    /// </summary>
    Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an occupation by id, including finalized and logically deleted rows.
    /// Used for reactivation and lifecycle operations that need the full record.
    /// </summary>
    Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing occupation.
    /// </summary>
    Task UpdateAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all persisted occupations, including finalized and logically deleted rows.
    /// Unlike <see cref="IReadOnlyRepository{T}.ListAllAsync"/>, this method does NOT
    /// apply soft-delete or active filters. Used when <c>includeHistory=true</c>.
    /// </summary>
    Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active occupation already exists for the given Puesto.
    /// Excludes the occupation with the specified <paramref name="excludingId"/> if provided.
    /// </summary>
    Task<bool> ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active occupation already exists for the given Persona + Puesto pair.
    /// Excludes the occupation with the specified <paramref name="excludingId"/> if provided.
    /// </summary>
    Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default);
}
