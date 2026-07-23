using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
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

    public async Task<PagedResult<HabilidadDto>> QueryAsync(
        HabilidadListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.QueryAsync(
            query.Search,
            query.Page,
            query.PageSize,
            query.Sort,
            query.Segmento,
            cancellationToken);

        return new PagedResult<HabilidadDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private static HabilidadDto MapToDto(Habilidad entity)
    {
        // El repo carga la navegación Categoria (LEFT JOIN CategoriasHabilidad
        // via Projection). Si la FK es NULL, Categoria queda en null y el
        // nombre proyectado es null también — wire consistente.
        return new HabilidadDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.CategoriaId,
            entity.Categoria?.Nombre);
    }
}