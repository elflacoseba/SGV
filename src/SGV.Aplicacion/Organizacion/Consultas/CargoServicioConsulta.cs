using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class CargoServicioConsulta(ICargoRepository repository)
    : ICargoServicioConsulta
{
    public async Task<IReadOnlyList<CargoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static CargoDto MapToDto(Cargo entity)
    {
        return new CargoDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.NivelId,
            entity.NivelCargo?.Nombre
        );
    }
}
