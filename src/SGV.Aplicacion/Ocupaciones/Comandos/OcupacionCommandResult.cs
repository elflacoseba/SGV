using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;

namespace SGV.Aplicacion.Ocupaciones.Comandos;

/// <summary>
/// Categorizes command-side failures for Ocupacion operations.
/// </summary>
public enum OcupacionErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by Ocupacion write operations.
/// </summary>
public sealed record OcupacionError(
    OcupacionErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of an Ocupacion write operation: either a success DTO or a typed error.
/// </summary>
public sealed record OcupacionCommandResult(
    bool IsSuccess,
    OcupacionDto? Value,
    OcupacionError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static OcupacionCommandResult Success(OcupacionDto value)
        => new(true, value, null);

    public static OcupacionCommandResult Failure(OcupacionError error)
        => new(false, null, error);

    public static OcupacionCommandResult Failure(
        OcupacionError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
