using SGV.Contracts.Comun;
using SGV.Contracts.Setup;

namespace SGV.Aplicacion.Setup;

/// <summary>
/// Tipo de error tipado para el setup one-time (issue #195). Combina
/// la categoría semántica <see cref="ErrorCategoria"/>, el código de
/// dominio <see cref="SetupErrorCode"/> y un <see cref="StatusCode"/>
/// opcional que el <c>SetupController</c> usa para mapear el HTTP.
/// </summary>
public sealed record SetupError(
    ErrorCategoria Categoria,
    SetupErrorCode Code,
    string Message,
    int? StatusCode = null);

/// <summary>
/// Resultado del setup one-time. Sigue el patrón canónico de los
/// <c>*CommandResult</c> de <c>SGV.Contracts</c>:
/// <c>IsSuccess</c> + <c>Value</c> (en éxito) + <c>Error</c> (en fallo)
/// + <c>FieldErrors</c> opcional (errores por campo de FluentValidation).
/// </summary>
public sealed record SetupCommandResult(
    bool IsSuccess,
    SetupResult? Value,
    SetupError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static SetupCommandResult Success(SetupResult value)
        => new(true, value, null);

    public static SetupCommandResult Failure(SetupError error)
        => new(false, null, error);

    public static SetupCommandResult Failure(
        SetupError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
