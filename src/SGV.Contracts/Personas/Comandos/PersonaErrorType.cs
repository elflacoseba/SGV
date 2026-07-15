namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Categorizes command-side failures for Persona operations.
/// </summary>
public enum PersonaErrorType
{
    NotFound,
    Conflict,
    Validation
}