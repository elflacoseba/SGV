using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Comandos;

/// <summary>
/// Categorizes command-side failures for Habilidad operations.
/// </summary>
public enum HabilidadErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by Habilidad write operations.
/// </summary>
public sealed record HabilidadError(
    HabilidadErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of a Habilidad write operation: either a success DTO or a typed error.
/// </summary>
public sealed record HabilidadCommandResult(
    bool IsSuccess,
    HabilidadDto? Value,
    HabilidadError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static HabilidadCommandResult Success(HabilidadDto value)
        => new(true, value, null);

    public static HabilidadCommandResult Failure(HabilidadError error)
        => new(false, null, error);

    public static HabilidadCommandResult Failure(
        HabilidadError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
