using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for Puesto operations.
/// </summary>
public enum PuestoErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by Puesto write operations.
/// </summary>
public sealed record PuestoError(
    PuestoErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of a Puesto write operation: either a success DTO or a typed error.
/// </summary>
public sealed record PuestoCommandResult(
    bool IsSuccess,
    PuestoDto? Value,
    PuestoError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static PuestoCommandResult Success(PuestoDto value)
        => new(true, value, null);

    public static PuestoCommandResult Failure(PuestoError error)
        => new(false, null, error);

    public static PuestoCommandResult Failure(
        PuestoError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
