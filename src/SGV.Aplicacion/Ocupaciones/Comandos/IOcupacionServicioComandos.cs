namespace SGV.Aplicacion.Ocupaciones.Comandos;

/// <summary>
/// Application service for Ocupacion write operations.
/// </summary>
public interface IOcupacionServicioComandos
{
    /// <summary>
    /// Creates a new occupation assignment.
    /// </summary>
    Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing active occupation.
    /// </summary>
    Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes an active occupation by setting its end date.
    /// </summary>
    Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logically deletes an active occupation.
    /// </summary>
    Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a finalized or logically deleted occupation.
    /// </summary>
    Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
