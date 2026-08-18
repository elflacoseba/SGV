using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Helper estático para construir el lookup Guid → <see cref="TipoDocumentoDto"/>
/// compartido entre <see cref="PersonaServicioConsulta"/> y
/// <see cref="Comandos.PersonaServicioComandos"/>. Centraliza la query al
/// catálogo para que ambos servicios devuelvan los mismos campos
/// denormalizados en sus DTOs (D-PE-01).
/// </summary>
public static class TipoDocumentoLookupBuilder
{
    /// <summary>
    /// Carga el catálogo de tipos de documento una vez por request y lo
    /// proyecta a un diccionario O(1) por Guid. Si el catálogo está vacío,
    /// devuelve un diccionario vacío (los campos denormalizados del DTO
    /// quedan <c>null</c>).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, TipoDocumentoDto>> BuildAsync(
        ITipoDocumentoCatalogoConsulta catalogo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var tipos = await catalogo.ListarAsync(cancellationToken).ConfigureAwait(false);
        return tipos.ToDictionary(t => t.Id);
    }
}