using SGV.Contracts.Vacantes.Comandos;

namespace SGV.Aplicacion.Vacantes.Comandos;

/// <summary>
/// Application service for Vacante write operations.
/// Encapsulates FluentValidation, reference/state checks, and the atomic
/// persistence bridge that keeps vacante mutations and
/// <c>HistorialEstadoVacante</c> inserts in a single EF transaction
/// (<c>design.md</c> §D-5). All commands are gated by FluentValidation
/// before any I/O happens; reference lookups (Puesto, EstadoVacante)
/// happen after validation and surface as
/// <see cref="SGV.Contracts.Comun.ErrorCategoria.NotFound"/> or
/// <see cref="SGV.Contracts.Comun.ErrorCategoria.Conflict"/> per the
/// canonical taxonomy.
/// </summary>
public interface IVacanteServicioComandos
{
    /// <summary>
    /// Opens a new vacante for the given <c>PuestoId</c>, enforcing the
    /// "one open vacante per puesto" rule via
    /// <see cref="Vacantes.Consultas.IVacanteRepository.ExistsAbiertaByPuestoAsync"/>.
    /// On success returns the created <see cref="SGV.Contracts.Vacantes.Consultas.Dtos.VacanteDetailDto"/>.
    /// </summary>
    Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a vacante to a new <c>EstadoVacanteId</c> and persists a
    /// matching <c>HistorialEstadoVacante</c> row in the same EF transaction
    /// (atomicidad, <c>design.md</c> §D-5). When the target state is
    /// terminal (<c>EsTerminal == true</c>) the domain sets
    /// <c>FechaCierre</c> automatically.
    /// </summary>
    Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the free-form <c>Observaciones</c> field of a vacante.
    /// Mirrors <c>Vacante.ActualizarObservaciones(string?)</c> on the
    /// domain (≤500 chars, null/empty/whitespace cleared).
    /// </summary>
    Task<VacanteCommandResult> ActualizarObservacionesAsync(
        Guid id,
        string? observaciones,
        CancellationToken cancellationToken = default);
}