namespace SGV.Aplicacion.Habilidades.Comandos;

/// <summary>
/// Application service for Habilidad write operations.
/// </summary>
public interface IHabilidadServicioComandos
{
    /// <summary>
    /// Creates a new Habilidad.
    /// </summary>
    Task<HabilidadCommandResult> CrearAsync(
        CrearHabilidadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing Habilidad.
    /// </summary>
    Task<HabilidadCommandResult> ActualizarAsync(
        Guid id,
        ActualizarHabilidadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a Habilidad.
    /// </summary>
    Task<HabilidadCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted Habilidad.
    /// </summary>
    Task<HabilidadCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
