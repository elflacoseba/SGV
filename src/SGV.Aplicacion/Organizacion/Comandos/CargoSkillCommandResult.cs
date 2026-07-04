using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for CargoSkill operations.
/// </summary>
public enum CargoSkillErrorType
{
    NotFound,
    Validation
}

/// <summary>
/// Typed error returned by CargoSkill write operations.
/// </summary>
public sealed record CargoSkillError(
    CargoSkillErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of a CargoSkill write operation: either a success DTO or a typed error.
/// When <see cref="FieldErrors"/> is populated the caller MUST surface the
/// per-field errors via a <c>ValidationProblemDetails</c> response (HTTP 400).
/// </summary>
public sealed record CargoSkillCommandResult(
    bool IsSuccess,
    CargoSkillDto? Value,
    CargoSkillError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static CargoSkillCommandResult Success(CargoSkillDto value)
        => new(true, value, null);

    public static CargoSkillCommandResult Failure(CargoSkillError error)
        => new(false, null, error);

    public static CargoSkillCommandResult Failure(
        CargoSkillError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}