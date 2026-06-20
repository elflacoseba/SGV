using SGV.Aplicacion.Personas.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Categorizes command-side failures for Persona operations.
/// </summary>
public enum PersonaErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by Persona write operations.
/// </summary>
public sealed record PersonaError(
    PersonaErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of a Persona write operation: either a success DTO or a typed error.
/// </summary>
public sealed record PersonaCommandResult(
    bool IsSuccess,
    PersonaDto? Value,
    PersonaError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static PersonaCommandResult Success(PersonaDto value)
        => new(true, value, null);

    public static PersonaCommandResult Failure(PersonaError error)
        => new(false, null, error);

    public static PersonaCommandResult Failure(
        PersonaError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
