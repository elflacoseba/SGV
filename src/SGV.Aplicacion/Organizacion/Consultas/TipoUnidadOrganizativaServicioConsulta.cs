using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class TipoUnidadOrganizativaServicioConsulta(ITipoUnidadOrganizativaRepository repository)
    : ITipoUnidadOrganizativaServicioConsulta
{
    public async Task<IReadOnlyList<TipoUnidadOrganizativaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<TipoUnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static TipoUnidadOrganizativaDto MapToDto(TipoUnidadOrganizativa entity)
    {
        return new TipoUnidadOrganizativaDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre
        );
    }
}
