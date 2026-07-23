using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only query service for the <c>CategoriaHabilidad</c> catalog
/// (issue migrar-campo-categoria-habilidades-a-tabla). Implemented in
/// <c>SGV.Infraestructura</c> (DI registration in <c>DependencyInjection.cs</c>).
/// </summary>
public interface ICategoriaHabilidadServicioConsulta
{
    /// <summary>
    /// Returns the catalog as DTOs, ordered by <c>Nombre</c> ascending.
    /// </summary>
    Task<IReadOnlyList<CategoriaHabilidadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single catalog row as DTO by id, or <c>null</c> if not found.
    /// </summary>
    Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}