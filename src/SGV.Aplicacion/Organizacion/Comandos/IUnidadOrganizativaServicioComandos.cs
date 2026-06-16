namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Application service for organizational-unit write operations.
/// </summary>
public interface IUnidadOrganizativaServicioComandos
{
    /// <summary>
    /// Creates a new organizational unit.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> CrearAsync(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing organizational unit.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the parent of an organizational unit.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an organizational unit.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted organizational unit.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<UnidadOrganizativaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
