using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Read-only query service for the <c>TipoDocumento</c> catalog (issue #147).
/// Implemented in <c>SGV.Infraestructura</c> (DI registration in
/// <c>DependencyInjection.cs</c>).
/// </summary>
public interface ITipoDocumentoCatalogoConsulta
{
    Task<IReadOnlyList<TipoDocumentoDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<TipoDocumentoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}
