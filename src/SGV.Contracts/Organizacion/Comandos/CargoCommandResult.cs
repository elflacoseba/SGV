using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for Cargo operations.
/// </summary>
/// <remarks>
/// Alineado 1-a-1 con <see cref="ErrorCategoria"/> vía
/// <see cref="SGV.Contracts.Comun.ErrorCategoriaMappers"/>. Los valores se
/// agregan al final del enum para preservar los ordinales de los miembros
/// existentes (NotFound = 0, Conflict = 1, Validation = 2).
/// </remarks>
public enum CargoErrorType
{
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden,
    Transport,
    Unexpected
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
