using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

public sealed class HabilidadServicioConsulta(IHabilidadRepository repository)
    : IHabilidadServicioConsulta
{
    public async Task<IReadOnlyList<HabilidadDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static HabilidadDto MapToDto(Habilidad entity)
    {
        return new HabilidadDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.Categoria
        );
    }
}
