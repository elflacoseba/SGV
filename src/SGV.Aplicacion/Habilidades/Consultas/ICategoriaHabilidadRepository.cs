using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only repository contract for the <c>CategoriaHabilidad</c> catalog
/// (issue migrar-campo-categoria-habilidades-a-tabla). No
/// <c>Add/Update/Delete</c>: the catalog is immutable per
/// <c>REQ-SPA-EVOLUTION-001</c> condición #1.
/// </summary>
public interface ICategoriaHabilidadRepository
{
    /// <summary>
    /// Returns all catalog rows ordered by <c>Codigo</c> ascending.
    /// </summary>
    Task<IReadOnlyList<CategoriaHabilidad>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single catalog row by id, or <c>null</c> if not found.
    /// </summary>
    Task<CategoriaHabilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}