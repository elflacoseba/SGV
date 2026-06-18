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
    /// Retrieves a unit by id including soft-deleted ones for update/reactivation.
    /// </summary>
    Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing organizational unit.
    /// </summary>
    Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an organizational unit.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted organizational unit.
    /// </summary>
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Returns true when the specified unit has any active (non-deleted) children.
    /// </summary>
    Task<bool> HasActiveChildrenAsync(Guid unidadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the specified unit has any active (non-deleted) associated puestos.
    /// </summary>
    Task<bool> HasActivePuestosAsync(Guid unidadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, paginated set of active organizational units and the total count matching the filters.
    /// </summary>
    Task<(IReadOnlyList<UnidadOrganizativa> Items, int TotalCount)> QueryAsync(
        string? search,
        Guid? tipoUnidadOrganizativaId,
        Guid? unidadPadreId,
        DateOnly? vigenteEn,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active organizational units with their TipoUnidadOrganizativa navigation loaded,
    /// ordered for tree construction.
    /// </summary>
    Task<IReadOnlyList<UnidadOrganizativa>> ListTreeAsync(CancellationToken cancellationToken = default);
}
