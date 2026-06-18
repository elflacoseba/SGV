using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only repository for NivelCargo catalog queries.
/// </summary>
public interface INivelCargoRepository : IReadOnlyRepository<NivelCargo>
{
    /// <summary>
    /// Retrieves a NivelCargo by its unique code.
    /// </summary>
    Task<NivelCargo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
