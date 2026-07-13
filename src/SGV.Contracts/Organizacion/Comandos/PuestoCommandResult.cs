using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for Puesto operations.
/// </summary>
[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]
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
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

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
