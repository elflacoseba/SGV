namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Application service for Puesto write operations.
/// </summary>
public interface IPuestoServicioComandos
{
    /// <summary>
    /// Creates a new Puesto.
    /// </summary>
    Task<PuestoCommandResult> CrearAsync(
        CrearPuestoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing Puesto.
    /// </summary>
    Task<PuestoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPuestoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a Puesto.
    /// </summary>
    Task<PuestoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted Puesto.
    /// </summary>
    Task<PuestoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
