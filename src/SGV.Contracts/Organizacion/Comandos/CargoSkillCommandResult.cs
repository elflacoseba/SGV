using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for CargoSkill operations.
/// </summary>
/// <remarks>
/// Los valores se agregan al final del enum para preservar los ordinales
/// de los miembros existentes (NotFound = 0, Validation = 1). Los nuevos
/// miembros cubren códigos HTTP que el helper del cliente web debe
/// distinguir para que la Razor Page de PR3b pueda elegir el mensaje
/// correcto:
/// <list type="bullet">
///   <item><description><c>Conflict</c> — 409, antecedente en
///   <see cref="CargoErrorType.Conflict"/> del agregado padre.</description></item>
///   <item><description><c>Unauthorized</c> — 401, sesión expirada o token
///   inválido; la página debe redirigir a login.</description></item>
///   <item><description><c>Forbidden</c> — 403, usuario autenticado sin rol
///   Administrador; la página debe mostrar acceso denegado.</description></item>
///   <item><description><c>Transport</c> — 5xx (error del backend) o
///   fallo de transporte propagado. La página muestra mensaje
///   recuperable.</description></item>
/// </list>
/// </remarks>
public enum CargoSkillErrorType
{
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
    Transport
}

/// <summary>
/// Typed error returned by CargoSkill write operations.
/// </summary>
public sealed record CargoSkillError(
    CargoSkillErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

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
