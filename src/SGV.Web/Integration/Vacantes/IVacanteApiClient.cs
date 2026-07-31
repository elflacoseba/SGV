using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Typed HTTP client for the Vacantes web module.
/// </summary>
public interface IVacanteApiClient
{
    /// <summary>Lists vacantes using the backend segment and server-side filters.</summary>
    Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a vacante detail, or <see langword="null"/> when it is unavailable.</summary>
    Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the read-only EstadoVacante catalog.</summary>
    Task<IReadOnlyList<EstadoVacanteDto>> ListarEstadosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates a vacante and returns its persisted detail.</summary>
    Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Changes a vacante state and optionally updates observations.</summary>
    Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default);
}
