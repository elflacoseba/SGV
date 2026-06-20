using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Repository contract for Persona read and write operations.
/// </summary>
public interface IPersonaRepository : IReadOnlyRepository<Persona>
{
    /// <summary>
    /// Adds a new persona.
    /// </summary>
    Task AddAsync(Persona persona, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted persona for update.
    /// </summary>
    Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a persona by id including soft-deleted ones for reactivation.
    /// </summary>
    Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing persona.
    /// </summary>
    Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a persona.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted persona.
    /// </summary>
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active persona already uses the given legajo.
    /// </summary>
    Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active persona already uses the given email.
    /// </summary>
    Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active persona already uses the given document.
    /// </summary>
    Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default);
}
