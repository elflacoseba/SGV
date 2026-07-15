using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Result of a Persona write operation: either a success DTO or a typed error
/// with optional field-level validation errors.
/// </summary>
public sealed record PersonaCommandResult(
    bool IsSuccess,
    PersonaDto? Value,
    PersonaError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
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