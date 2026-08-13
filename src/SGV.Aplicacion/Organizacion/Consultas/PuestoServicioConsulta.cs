using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class PuestoServicioConsulta(IPuestoRepository repository)
    : IPuestoServicioConsulta
{
    public async Task<IReadOnlyList<PuestoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    public async Task<PagedResult<PuestoDto>> QueryAsync(
        PuestoListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.QueryAsync(
            query.Search,
            query.Page,
            query.PageSize,
            query.Sort,
            query.Segmento,
            cancellationToken);

        return new PagedResult<PuestoDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<IReadOnlyList<PuestoDto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListarDisponiblesAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    private static PuestoDto MapToDto(Puesto entity)
    {
        return new PuestoDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.UnidadOrganizativaId,
            entity.UnidadOrganizativa.Nombre,
            entity.CargoId,
            entity.Cargo.Nombre,
            entity.PuestoSuperiorId
        );
    }
}
