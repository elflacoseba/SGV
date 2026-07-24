using System.Net;
using SGV.Contracts.Setup;

namespace SGV.Web.Integration.Setup;

/// <summary>
/// Tipo de error tipado del shell web para el setup one-time (issue
/// #195). Espejo del <c>SetupError</c> de
/// <c>SGV.Aplicacion.Setup</c> (al que el shell web no puede
/// referenciar por la regla "Web depende sólo de Contracts"); la
/// diferencia es que este tipo NO vive en
/// <c>SGV.Aplicacion</c> y por lo tanto puede ser consumido por la
/// Razor Page sin romper la arquitectura Clean.
/// </summary>
public sealed record SetupHttpError(
    SetupErrorCode Code,
    string Message,
    HttpStatusCode? StatusCode = null);

/// <summary>
/// Resultado HTTP del <c>POST /api/v1/setup</c>. Sigue el patrón
/// canónico de los <c>*CommandResult</c> del shell web
/// (<c>IsSuccess</c> + <c>Value</c> en éxito + <c>Error</c> en
/// fallo + <c>FieldErrors</c> opcional). Las factorías
/// <see cref="Success"/> y <see cref="Failure"/> siguen la firma de
/// los <c>CommandResult.Success</c>/<c>Failure</c> vigentes.
/// </summary>
public sealed record SetupHttpResult(
    bool IsSuccess,
    SetupResult? Value,
    SetupHttpError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static SetupHttpResult Success(SetupResult value)
        => new(true, value, null);

    public static SetupHttpResult Failure(SetupHttpError error)
        => new(false, null, error);

    public static SetupHttpResult Failure(
        SetupHttpError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
