using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class UnidadOrganizativaServicioConsulta(IUnidadOrganizativaRepository repository)
    : IUnidadOrganizativaServicioConsulta
{
    public async Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static UnidadOrganizativaDto MapToDto(UnidadOrganizativa entity)
    {
        return new UnidadOrganizativaDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.TipoUnidad,
            entity.Descripcion,
            entity.VigenteDesde,
            entity.VigenteHasta,
            entity.UnidadPadreId
        );
    }
}
