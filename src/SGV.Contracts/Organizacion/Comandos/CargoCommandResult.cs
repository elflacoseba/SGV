using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for Cargo operations.
/// </summary>
[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]
public enum CargoErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by Cargo write operations.
/// </summary>
public sealed record CargoError(
    CargoErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

/// <summary>
/// Result of a Cargo write operation: either a success DTO or a typed error.
/// </summary>
public sealed record CargoCommandResult(
    bool IsSuccess,
    CargoDto? Value,
    CargoError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static CargoCommandResult Success(CargoDto value)
        => new(true, value, null);

    public static CargoCommandResult Failure(CargoError error)
        => new(false, null, error);

    public static CargoCommandResult Failure(
        CargoError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
