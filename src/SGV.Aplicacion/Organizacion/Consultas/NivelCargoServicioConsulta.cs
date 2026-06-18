using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class NivelCargoServicioConsulta(INivelCargoRepository repository)
    : INivelCargoServicioConsulta
{
    public async Task<IReadOnlyList<NivelCargoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<NivelCargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static NivelCargoDto MapToDto(NivelCargo entity)
    {
        return new NivelCargoDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.ValorNumerico,
            entity.Orden
        );
    }
}
