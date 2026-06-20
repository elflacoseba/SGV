namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Application service for Persona write operations.
/// </summary>
public interface IPersonaServicioComandos
{
    /// <summary>
    /// Creates a new Persona.
    /// </summary>
    Task<PersonaCommandResult> CrearAsync(
        CrearPersonaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable fields of an existing Persona.
    /// </summary>
    Task<PersonaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPersonaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a Persona.
    /// </summary>
    Task<PersonaCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted Persona.
    /// </summary>
    Task<PersonaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
