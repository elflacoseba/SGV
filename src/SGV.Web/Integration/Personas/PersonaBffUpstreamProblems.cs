using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Helper que traduce los fallos upstream del cliente tipado de Personas
/// (<see cref="IPersonaApiClient"/>) en respuestas <see cref="ProblemDetails"/>
/// estables para el shell web. Usado por el endpoint BFF
/// <c>/api/v1/personas/consulta</c> de <c>SGV.Web</c>.
/// </summary>
/// <remarks>
/// <para>
/// La taxonomía distingue tres modos de falla y emite una URN
/// <c>type</c> distinta para cada uno. Las URN siguen el patrón
/// <c>urn:sgv:errors:bff/&lt;categoría&gt;</c> para que clientes externos
/// puedan discriminar sin parsear <c>title</c> ni <c>detail</c>.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="UpstreamUnavailableType"/>: <see cref="HttpRequestException"/>
/// propagada desde <c>HttpClient</c> (DNS fail, conexión rechazada,
/// reset de socket, etc.). Status <c>502 Bad Gateway</c>.
/// </description></item>
/// <item><description>
/// <see cref="UpstreamTimeoutType"/>: <see cref="TaskCanceledException"/>
/// cuando el <c>CancellationToken</c> del request NO fue disparado por el
/// cliente (es decir, la excepción proviene del timeout del <c>HttpClient</c>).
/// Status <c>502 Bad Gateway</c>.
/// </description></item>
/// <item><description>
/// <see cref="ClientCancelledType"/>: <see cref="TaskCanceledException"/>
/// cuando el <c>CancellationToken</c> del request fue disparado por el
/// cliente (<see cref="HttpContext.RequestAborted"/>). Status <c>502 Bad Gateway</c>
/// pero con <c>type</c>/<c>title</c>/<c>detail</c> distintos para que la UI
/// pueda distinguir una cancelación local de un timeout upstream real.
/// </description></item>
/// </list>
/// <para>
/// Cada respuesta emite además <see cref="ILogger.LogError"/> con un scope
/// estructurado que contiene <c>Search</c>, <c>Sort</c>, <c>Segmento</c>,
/// <c>CorrelationId</c> (<see cref="HttpContext.TraceIdentifier"/>) y la
/// categoría de error. Esos pares llegan al pipeline de logging del host
/// como <c>IReadOnlyCollection&lt;KeyValuePair&lt;string, object&gt;&gt;</c>,
/// preservando el contrato observable que <c>LoggerMessage</c> y los
/// sinks estructurados esperan.
/// </para>
/// <para>
/// El helper NO captura <see cref="Exception"/> genérica: solo procesa
/// <see cref="HttpRequestException"/> y <see cref="TaskCanceledException"/>.
/// El caller debe enrutarlas explícitamente; cualquier otra excepción
/// propaga hacia el handler global de ASP.NET Core, que devolverá
/// <c>500 Internal Server Error</c>.
/// </para>
/// </remarks>
public static class PersonaBffUpstreamProblems
{
    /// <summary>URN estable para fallos de red upstream (HTTP request errors).</summary>
    public const string UpstreamUnavailableType = "urn:sgv:errors:bff/upstream-unavailable";

    /// <summary>URN estable para timeouts upstream (TCE sin cancel del cliente).</summary>
    public const string UpstreamTimeoutType = "urn:sgv:errors:bff/upstream-timeout";

    /// <summary>URN estable para cancelaciones iniciadas por el cliente.</summary>
    public const string ClientCancelledType = "urn:sgv:errors:bff/client-cancelled";

    private const string KindUpstreamUnavailable = "UpstreamUnavailable";
    private const string KindUpstreamTimeout = "UpstreamTimeout";
    private const string KindClientCancelled = "ClientCancelled";

    /// <summary>
    /// Construye el <see cref="ProblemDetails"/> y emite el log
    /// estructurado para una falla de <see cref="IPersonaApiClient.QueryAsync"/>.
    /// </summary>
    /// <param name="httpContext">Contexto HTTP del request entrante.</param>
    /// <param name="logger">Logger al que se emite el <see cref="LogLevel.Error"/>.</param>
    /// <param name="query">Query que disparó la falla. Se usa para poblar el scope estructurado.</param>
    /// <param name="exception">Excepción nativa propagada por el cliente tipado.</param>
    /// <param name="clientCancelled">
    /// <c>true</c> cuando el <see cref="CancellationToken"/> del request ya
    /// estaba cancelado antes de propagarse la excepción. Determina si la
    /// respuesta debe etiquetarse como cancelación local (<see cref="ClientCancelledType"/>)
    /// en lugar de timeout upstream (<see cref="UpstreamTimeoutType"/>).
    /// </param>
    public static IResult Build(
        HttpContext httpContext,
        ILogger logger,
        PersonaListQuery query,
        Exception exception,
        bool clientCancelled)
    {
        var (typeUri, title, detail, kind) = Classify(exception, clientCancelled);

        using (logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Search"] = query.Search,
            ["Sort"] = query.Sort,
            ["Segmento"] = query.Segmento.ToString(),
            ["CorrelationId"] = httpContext.TraceIdentifier,
            ["UpstreamErrorKind"] = kind
        }))
        {
            logger.LogError(exception, "BFF /api/v1/personas/consulta falló: {UpstreamErrorKind}", kind);
        }

        var problem = new ProblemDetails
        {
            Type = typeUri,
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status502BadGateway,
            Instance = httpContext.Request.Path
        };
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }

        return Results.Json(
            problem,
            statusCode: StatusCodes.Status502BadGateway,
            contentType: "application/problem+json");
    }

    private static (string Type, string Title, string Detail, string Kind) Classify(
        Exception exception, bool clientCancelled)
    {
        if (clientCancelled)
        {
            return (
                ClientCancelledType,
                "Cliente canceló la consulta",
                "El cliente cerró la conexión antes de que la API upstream respondiera.",
                KindClientCancelled);
        }

        if (exception is TaskCanceledException)
        {
            return (
                UpstreamTimeoutType,
                "Timeout al consultar la API upstream",
                "La consulta a la API upstream excedió el tiempo máximo de espera configurado.",
                KindUpstreamTimeout);
        }

        // HttpRequestException (network, DNS, socket reset, etc.) o cualquier
        // otra excepción que el caller haya enrutado a este helper. La rama
        // genérica del upstream se modela como no-disponible.
        return (
            UpstreamUnavailableType,
            "API upstream no disponible",
            "La API upstream no responde o rechazó la conexión.",
            KindUpstreamUnavailable);
    }
}