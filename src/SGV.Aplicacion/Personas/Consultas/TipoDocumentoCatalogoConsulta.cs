using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

public sealed class TipoDocumentoCatalogoConsulta(ITipoDocumentoRepository repository)
    : ITipoDocumentoCatalogoConsulta
{
    public async Task<IReadOnlyList<TipoDocumentoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<TipoDocumentoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static TipoDocumentoDto MapToDto(TipoDocumento entity)
    {
        return new TipoDocumentoDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.PatronValidacion,
            entity.LongitudMinima,
            entity.LongitudMaxima);
    }
}
