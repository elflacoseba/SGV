namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Application service for Cargo write operations.
/// </summary>
public interface ICargoServicioComandos
{
    /// <summary>
    /// Creates a new Cargo.
    /// </summary>
    Task<CargoCommandResult> CrearAsync(
        CrearCargoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing Cargo.
    /// </summary>
    Task<CargoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarCargoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a Cargo.
    /// </summary>
    Task<CargoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted Cargo.
    /// </summary>
    Task<CargoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
