using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Implementation of <see cref="INivelHabilidadServicioConsulta"/>. Maps
/// the NivelHabilidad domain entities to <see cref="NivelHabilidadDto"/>
/// preserving the catalog fields (Codigo / Nombre / ValorNumerico / Orden).
/// The repository guarantees the list is ordered by <c>Orden</c> ascending,
/// so this service does not re-sort.
/// </summary>
public sealed class NivelHabilidadServicioConsulta(INivelHabilidadRepository repository)
    : INivelHabilidadServicioConsulta
{
    public async Task<IReadOnlyList<NivelHabilidadDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<NivelHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static NivelHabilidadDto MapToDto(NivelHabilidad entity)
    {
        return new NivelHabilidadDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.ValorNumerico,
            entity.Orden);
    }
}