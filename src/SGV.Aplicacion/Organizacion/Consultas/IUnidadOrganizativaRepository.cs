using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Repository contract for UnidadOrganizativa read and write operations.
/// </summary>
public interface IUnidadOrganizativaRepository : IReadOnlyRepository<UnidadOrganizativa>
{
    /// <summary>
    /// Adds a new organizational unit.
    /// </summary>
    Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted unit for update.
    /// </summary>
    Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing organizational unit.
    /// </summary>
    Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an organizational unit.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active unit already uses the given code.
    /// </summary>
    Task<bool> ExistsActiveCodeAsync(
        string codigo,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when <paramref name="candidateDescendantId"/> is a descendant of <paramref name="ancestorId"/>.
    /// </summary>
    Task<bool> IsDescendantAsync(
        Guid candidateDescendantId,
        Guid ancestorId,
        CancellationToken cancellationToken = default);
}
