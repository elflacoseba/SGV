using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only implementation that delegates to <see cref="ICategoriaHabilidadRepository"/>
/// and maps the domain entity to the consumer-safe <see cref="CategoriaHabilidadDto"/>.
/// </summary>
public sealed class CategoriaHabilidadServicioConsulta(ICategoriaHabilidadRepository repository)
    : ICategoriaHabilidadServicioConsulta
{
    public async Task<IReadOnlyList<CategoriaHabilidadDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entidades = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entidades.Select(MapToDto).ToList();
    }

    public async Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entidad = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entidad is not null ? MapToDto(entidad) : null;
    }

    private static CategoriaHabilidadDto MapToDto(Dominio.Habilidades.CategoriaHabilidad entity)
    {
        return new CategoriaHabilidadDto(entity.Id, entity.Codigo, entity.Nombre);
    }
}