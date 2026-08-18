using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Personas.Consultas.Dtos;
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
    /// Checks whether an active persona already uses the given document
    /// (issue #147: TipoDocumentoId FK + NumeroDocumento).
    /// </summary>
    Task<bool> ExistsActiveDocumentoAsync(Guid tipoDocumentoId, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, paginated set of personas for the requested segment
    /// (active or soft-deleted) and the total count matching the filters.
    /// Search applies to <c>Legajo|Nombres|Apellidos|Email|NumeroDocumento</c>
    /// case-insensitively. <paramref name="sort"/> is applied server-side
    /// BEFORE pagination so page boundaries are consistent with the visible
    /// ordering (e.g. <c>apellidos_desc</c> returns Z→A on every page).
    /// </summary>
    /// <param name="soloSinUsuario">
    /// When <c>true</c>, Activas are restricted to personas with no
    /// <c>AspNetUsers.PersonaId</c> pointing at them (anti-join). When
    /// <c>null</c> or <c>false</c>, the flag is ignored and the
    /// previous behavior is preserved (back-compat). Combined with
    /// <see cref="PersonaSegmentoListado.Eliminadas"/>, returns an
    /// empty result without invoking the anti-join.
    /// </param>
    /// <remarks>
    /// Sort values supported: <c>legajo_asc/desc</c>, <c>apellidos_asc/desc</c>,
    /// <c>nombres_asc/desc</c>, <c>email_asc/desc</c>. Any other value falls
    /// back to the default ordering (<c>apellidos_asc</c>).
    /// </remarks>
    Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
        CancellationToken cancellationToken = default,
        bool? soloSinUsuario = null);

    /// <summary>
    /// Server-side typeahead search (D-PE-03). Returns the first
    /// <paramref name="take"/> active personas matching
    /// <paramref name="search"/> substring (case-insensitive over
    /// <c>Legajo|Nombres|Apellidos|Email|NumeroDocumento</c>), ordered
    /// by <c>Apellidos, Nombres</c>. When <paramref name="search"/> is
    /// null/empty, returns up to <paramref name="take"/> active personas
    /// ordered by the same criteria.
    /// <para>
    /// Replaces the legacy "GET /api/v1/personas sin paginar" usado por el
    /// typeahead web, que pesaba ~100 KB para 500 personas activas y
    /// deforma la experiencia cuando el dataset crece. La búsqueda
    /// server-side evita N round-trips HTTP y la carga inicial de todo el
    /// catálogo en el navegador.
    /// </para>
    /// <para>
    /// <paramref name="soloSinUsuario"/> filtra opcionalmente las personas
    /// sin <c>AspNetUsers.PersonaId</c> asociado (anti-join reutilizado
    /// del método <see cref="QueryAsync"/>).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Persona>> BuscarAsync(
        string? search,
        int take,
        bool? soloSinUsuario = null,
        CancellationToken cancellationToken = default);
}
