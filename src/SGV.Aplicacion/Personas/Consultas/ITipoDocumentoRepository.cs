using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Read-only repository for the <c>TipoDocumento</c> catalog queries
/// consumed by application services (issue #147).
/// </summary>
public interface ITipoDocumentoRepository : IReadOnlyRepository<TipoDocumento>
{
    /// <summary>
    /// Retrieves a <see cref="TipoDocumento"/> by its unique code.
    /// </summary>
    Task<TipoDocumento?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
