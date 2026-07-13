using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Helper estático que traduce una respuesta HTTP no exitosa a la taxonomía
/// común <see cref="ErrorCategoria"/>. Es la única fuente de verdad para la
/// matriz status→categoría en <c>SGV.Web</c>; los clientes tipados
/// (<see cref="Habilidades.HabilidadApiClient"/>,
/// <see cref="Organizacion.CargoApiClient"/>,
/// <see cref="Organizacion.PuestosApiClient"/>,
/// <see cref="Organizacion.UnidadOrganizativaApiClient"/>) delegan acá en
/// lugar de mantener matrices <c>switch</c> privadas.
/// </summary>
/// <remarks>
/// <para>
/// Sólo opera sobre <see cref="HttpResponseMessage"/> — NO consume
/// excepciones nativas (<see cref="HttpRequestException"/>,
/// <see cref="TaskCanceledException"/>, etc). Esas se propagan al
/// consumidor y <see cref="TransportFailureClassifier.IsDnsFailure"/>
/// queda en el <c>PageModel</c> para discriminarlas.
/// </para>
/// <para>
/// La matriz cumple el requisito REQ-2 (Slice 2 del change #125):
/// </para>
/// <list type="bullet">
///   <item><description><c>400, 422</c> → <see cref="ErrorCategoria.Validation"/></description></item>
///   <item><description><c>401</c> → <see cref="ErrorCategoria.Unauthorized"/></description></item>
///   <item><description><c>403</c> → <see cref="ErrorCategoria.Forbidden"/></description></item>
///   <item><description><c>404</c> → <see cref="ErrorCategoria.NotFound"/></description></item>
///   <item><description><c>408, 500, 502, 503, 504</c> → <see cref="ErrorCategoria.Transport"/></description></item>
///   <item><description><c>409</c> → <see cref="ErrorCategoria.Conflict"/></description></item>
///   <item><description>Resto no 2xx (incluye 3xx, 1xx) → <see cref="ErrorCategoria.Unexpected"/></description></item>
/// </list>
/// <para>
/// Los códigos <c>code</c>/<c>message</c> provistos por el backend (a través
/// de <paramref name="problem"/>) tienen precedencia sobre los defaults; si
/// <see cref="ApiProblemReader.Result.Title"/> viene vacío, el mapper usa
/// el default documentado en el design §5.4.
/// </para>
/// </remarks>
public static class CommandResultMapper
{
    /// <summary>
    /// Mapea una respuesta HTTP y su <see cref="ApiProblemReader.Result"/>
    /// asociado a la taxonomía común <see cref="ErrorCategoria"/>.
    /// </summary>
    /// <param name="response">Respuesta HTTP leída por el cliente. Sólo se
    /// consulta <see cref="HttpResponseMessage.StatusCode"/>.</param>
    /// <param name="problem">Resultado del parseo del body (puede tener
    /// <c>Title</c>/<c>Detail</c> nulos cuando el body no es un
    /// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> válido).</param>
    /// <returns>Tupla con la categoría, defaults priorizados sobre el body,
    /// y status code numérico preservado para diagnóstico.</returns>
    public static (ErrorCategoria Categoria, string Code, string Message, int? StatusCode) Map(
        HttpResponseMessage response,
        ApiProblemReader.Result problem)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(problem);

        var status = (int)response.StatusCode;
        var (categoria, defaultCode, defaultMessage) = ResolveCategoria(status);

        var code = string.IsNullOrEmpty(problem.Title) ? defaultCode : problem.Title!;
        var message = string.IsNullOrEmpty(problem.Detail) ? defaultMessage : problem.Detail!;

        return (categoria, code, message, status);
    }

    /// <summary>
    /// Resuelve la tupla <c>(ErrorCategoria, default Code, default Message)</c>
    /// para un status HTTP. Switch sin <c>default:</c> explícito: cualquier
    /// status no listado cae en <see cref="ErrorCategoria.Unexpected"/>
    /// mediante el <c>_ =></c> final.
    /// </summary>
    private static (ErrorCategoria Categoria, string Code, string Message) ResolveCategoria(int status) => status switch
    {
        400 or 422 => (ErrorCategoria.Validation, "BadRequest", "Solicitud inválida."),
        401 => (ErrorCategoria.Unauthorized, "Unauthorized", "Su sesión expiró. Vuelva a iniciar sesión."),
        403 => (ErrorCategoria.Forbidden, "Forbidden", "Acceso denegado."),
        404 => (ErrorCategoria.NotFound, "NotFound", "Recurso no encontrado."),
        408 => (ErrorCategoria.Transport, "TransportError", "El servicio no respondió correctamente. Intentá nuevamente."),
        409 => (ErrorCategoria.Conflict, "Conflict", "Conflicto."),
        500 or 502 or 503 or 504 => (ErrorCategoria.Transport, "TransportError", "El servicio no respondió correctamente. Intentá nuevamente."),
        _ => (ErrorCategoria.Unexpected, "Unexpected", "Respuesta inesperada del servidor.")
    };
}
