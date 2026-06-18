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

    public async Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(
        UnidadOrganizativaQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.QueryAsync(
            query.Search,
            query.TipoUnidadOrganizativaId,
            query.UnidadPadreId,
            query.VigenteEn,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<UnidadOrganizativaDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await repository.ListTreeAsync(cancellationToken);
        return BuildTree(all, null);
    }

    private static List<UnidadOrganizativaTreeNodeDto> BuildTree(
        IReadOnlyList<UnidadOrganizativa> all, Guid? parentId)
    {
        return all
            .Where(u => u.UnidadPadreId == parentId)
            .Select(u => new UnidadOrganizativaTreeNodeDto(
                u.Id,
                u.Codigo,
                u.Nombre,
                u.TipoUnidadOrganizativaId,
                u.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
                BuildTree(all, u.Id)))
            .ToList();
    }

    private static UnidadOrganizativaDto MapToDto(UnidadOrganizativa entity)
    {
        return new UnidadOrganizativaDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.TipoUnidadOrganizativaId,
            entity.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
            entity.Descripcion,
            entity.VigenteDesde,
            entity.VigenteHasta,
            entity.UnidadPadreId
        );
    }
}
