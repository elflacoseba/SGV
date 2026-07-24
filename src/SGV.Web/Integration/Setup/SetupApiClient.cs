using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Setup;
using SGV.Web.Integration.Common;

// NOTA arquitectónica: SGV.Web no puede referenciar SGV.Aplicacion
// (regla Clean Architecture: Web depende sólo de Contracts). Por
// eso este cliente deserializa el cuerpo de la respuesta con
// System.Text.Json directamente, sin importar SetupCommandResult.

namespace SGV.Web.Integration.Setup;

/// <summary>
/// Implementación HTTP de <see cref="ISetupApiClient"/> contra los
/// endpoints anónimos del setup one-time (issue #195). El shell web
/// la registra como typed client en
/// <c>src/SGV.Web/Program.cs</c> SIN el
/// <see cref="Auth.ApiBearerTokenHandler"/>, porque los endpoints de
/// setup son públicos.
/// </summary>
/// <remarks>
/// <para>
/// El status (<c>GET /api/v1/setup/status</c>) se cachea con TTL
/// absoluto de 30s (design §2.3) y aplica fail-open: si la API está
/// caída, devuelve <c>RequiresSetup=false</c> para no romper el
/// acceso al sistema completo.
/// </para>
/// <para>
/// El catálogo de <c>TipoDocumento</c> (<c>GET /api/v1/tipos-documento</c>)
/// NO se cachea en este cliente porque la cantidad de filas es muy
/// reducida (4) y la Razor Page sólo lo pide en el render del GET
/// inicial de <c>/auth/setup</c>. Si en el futuro se vuelve un
/// hotspot, se puede agregar otro <see cref="IMemoryCache"/> con TTL
/// más largo.
/// </para>
/// </remarks>
public sealed class SetupApiClient(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<SetupApiClient> logger) : ISetupApiClient
{
    /// <summary>
    /// Clave del cache en memoria para el status. Es internal en
    /// lugar de private porque los tests la limpian para forzar un
    /// round-trip fresco. El assembly SGV.Tests accede via
    /// InternalsVisibleTo (ver <c>SetupApiClientTests.ObtenerEstadoAsync_FallaYRecuperacion_RecacheaValorReal</c>).
    /// </summary>
    internal const string StatusCacheKey = "setup:status";

    private static readonly TimeSpan StatusTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<SetupStatusResponse>(StatusCacheKey, out var hit) && hit is not null)
        {
            return hit;
        }

        try
        {
            using var response = await httpClient
                .GetAsync(SetupApiRoutes.Status, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var status = await response.Content
                .ReadFromJsonAsync<SetupStatusResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false) ?? new SetupStatusResponse(false);

            cache.Set(StatusCacheKey, status, StatusTtl);
            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Fallo al consultar estado de setup; fail-open devolviendo RequiresSetup=false");
            // Cache negativo con TTL corto: durante una outage evitamos
            // golpear la API en cada request, pero nos recuperamos rápido
            // cuando el servicio vuelve (10s vs 30s del cache positivo).
            var fallback = new SetupStatusResponse(false);
            cache.Set(StatusCacheKey, fallback, NegativeTtl);
            return fallback;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .GetAsync("/api/v1/tipos-documento", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<TipoDocumentoDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PostAsJsonAsync("/api/v1/setup", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            // El backend (PR #1) responde con un
            // SetupCommandResult JSON { isSuccess, value: SetupResult,
            // error, fieldErrors }. SGV.Web no puede referenciar
            // SetupCommandResult (Clean Architecture: Web depende
            // sólo de Contracts), por eso leemos el body con
            // System.Text.Json y extraemos manualmente los campos
            // que necesitamos.
            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("value", out var valueElement)
                    && valueElement.ValueKind == JsonValueKind.Object)
                {
                    var personaId = valueElement.TryGetProperty("personaId", out var pid)
                        ? pid.GetGuid()
                        : Guid.Empty;
                    var userId = valueElement.TryGetProperty("userId", out var uid)
                        && uid.ValueKind == JsonValueKind.String
                        ? uid.GetString() ?? string.Empty
                        : string.Empty;
                    var userName = valueElement.TryGetProperty("userName", out var un)
                        && un.ValueKind == JsonValueKind.String
                        ? un.GetString() ?? string.Empty
                        : string.Empty;

                    return SetupHttpResult.Success(new SetupResult(personaId, userId, userName));
                }

                // La propiedad "value" no existe o no es un objeto — probablemente
                // el contrato del backend cambió (ej: renombraron la propiedad
                // a "data" o "result"). Loggear para diagnóstico.
                logger.LogWarning(
                    "POST /api/v1/setup devolvió 200 sin propiedad 'value'. Contrato del backend cambió? Cuerpo: {Body}",
                    body);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "SGV.Api devolvió 200 con cuerpo no parseable en POST /api/v1/setup");
            }

            return SetupHttpResult.Failure(new SetupHttpError(
                SetupErrorCode.TransaccionFallida,
                "El servidor respondió 200 con cuerpo inválido.",
                HttpStatusCode.InternalServerError));
        }

        return await ToSetupHttpResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Traduce una respuesta HTTP no exitosa a un
    /// <see cref="SetupHttpResult"/> reusando los helpers comunes del
    /// shell web (<see cref="ApiProblemReader"/> para parsear el
    /// cuerpo + manual map del <c>ProblemDetails.Title</c> a un
    /// <see cref="SetupErrorCode"/>). Los códigos válidos están
    /// definidos en el switch de
    /// <see cref="MapTitleToErrorCode"/>; un título no reconocido
    /// colapsa a <see cref="SetupErrorCode.TransaccionFallida"/>.
    /// </summary>
    private static async Task<SetupHttpResult> ToSetupHttpResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var code = MapTitleToErrorCode(parsed.Title, response.StatusCode);
        var message = parsed.Detail ?? parsed.Title ?? "Error desconocido al crear el administrador.";
        var error = new SetupHttpError(code, message, response.StatusCode);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return SetupHttpResult.Failure(error, parsed.FieldErrors);
        }

        return SetupHttpResult.Failure(error);
    }

    /// <summary>
    /// Mapea el título del <c>ProblemDetails</c> devuelto por la API
    /// al <see cref="SetupErrorCode"/> correspondiente. Los títulos
    /// coinciden con <c>SetupErrorCode.ToString()</c> en la API
    /// (controller <c>BuildProblem</c>), excepto el caso rate limit
    /// que responde 429 sin <c>ProblemDetails</c> — en ese caso el
    /// título es null y caemos al código genérico
    /// <see cref="SetupErrorCode.TransaccionFallida"/> con el status
    /// real del HTTP response.
    /// </summary>
    private static SetupErrorCode MapTitleToErrorCode(string? title, HttpStatusCode statusCode)
    {
        if (Enum.TryParse<SetupErrorCode>(title, ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        // Fallback por status code conocido: la API usa
        // ProblemDetails con title="SetupYaCompletado" en 409, pero
        // un 5xx sin body siempre colapsa a TransaccionFallida.
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => SetupErrorCode.TransaccionFallida,
            HttpStatusCode.Conflict => SetupErrorCode.SetupYaCompletado,
            HttpStatusCode.BadRequest => SetupErrorCode.DatosInvalidos,
            _ => SetupErrorCode.TransaccionFallida
        };
    }
}
