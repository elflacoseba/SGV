using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Centralizes the safe-with-fallback parsing of <see cref="ProblemDetails"/>
/// and <see cref="ValidationProblemDetails"/> from an
/// <see cref="HttpResponseMessage"/> body.
/// </summary>
/// <remarks>
/// <para>
/// Pre-issue-#102 each typed HTTP client had its own near-identical
/// copy of this logic with slightly different default fallbacks:
/// </para>
/// <list type="bullet">
///   <item><description><c>CargoApiClient.ToCommandResultAsync</c> wrapped
///   every <c>ReadFromJsonAsync</c> in a try/catch and emitted a
///   <c>Failure(Validation, "Unexpected", …)</c> on non-JSON,</description></item>
///   <item><description><c>HabilidadApiClient.TryReadProblemDetailsAsync</c>
///   absorbed <c>JsonException</c> / <c>NotSupportedException</c> and
///   returned null,</description></item>
///   <item><description><c>CargoApiClient.ReadSkillProblemAsync</c> shared
///   part of the helper with <c>DeleteSkillAsync</c> but lived next to the
///   PUT branch.</description></item>
/// </list>
/// <para>
/// The reader is the single source of truth for: (a) which exceptions are
/// absorbed vs. propagated, (b) when a body is treated as
/// <see cref="ValidationProblemDetails"/> vs. <see cref="ProblemDetails"/>,
/// (c) how the title/detail/fieldErrors are exposed to the caller. Per-client
/// logic only maps the <see cref="Result"/> shape to the typed
/// <c>CommandResult</c> (preserving the historical per-module code/message
/// defaults that the existing tests pin).
/// </para>
/// </remarks>
public static class ApiProblemReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Outcome of parsing an HTTP error response body.
    /// <see cref="FieldErrors"/> is non-null whenever the response body is
    /// a <see cref="ValidationProblemDetails"/> (i.e., carries the
    /// <c>errors</c> key). The dictionary is populated when the backend
    /// delivered per-field errors, or empty when the shape is
    /// <c>ValidationProblemDetails</c> with no <c>errors</c>. Empty bodies,
    /// plain <see cref="ProblemDetails"/> and unparseable payloads yield a
    /// null <see cref="FieldErrors"/>.
    /// </summary>
    public sealed record Result(
        HttpStatusCode StatusCode,
        string? Title,
        string? Detail,
        IReadOnlyDictionary<string, string[]>? FieldErrors);

    /// <summary>
    /// Reads the response body and produces a <see cref="Result"/>. Returns a
    /// safe fallback (Title/Detail null) when the body is missing, empty or
    /// not a valid <see cref="ProblemDetails"/> payload — letting the caller
    /// apply its own defaults without leaking a native
    /// <see cref="JsonException"/> to the UI.
    /// </summary>
    /// <param name="response">HTTP response to inspect. Not disposed by this
    /// helper; ownership stays with the caller.</param>
    /// <param name="cancellationToken">Cancellation token. The token is
    /// checked before any body read so a pre-canceled token never touches
    /// the response stream.</param>
    public static async Task<Result> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // HttpResponseMessage.Content is non-null by default, but a caller
        // (or a test double) can assign null explicitly. Guard so the reader
        // degrades to a safe fallback instead of throwing a
        // NullReferenceException before it can even inspect the body.
        if (response.Content is null)
        {
            return new Result(response.StatusCode, Title: null, Detail: null, FieldErrors: null);
        }

        // Buffer the body once. ReadFromJsonAsync consumes the underlying
        // stream, so we cannot reuse it across a ValidationProblemDetails
        // and a ProblemDetails parse. ReadAsStringAsync returns the full
        // payload without forcing a specific shape.
        var body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            return new Result(response.StatusCode, Title: null, Detail: null, FieldErrors: null);
        }

        // 1) Detectar si el cuerpo tiene la clave "errors" (única
        //    diferencia serializable entre un ValidationProblemDetails y un
        //    ProblemDetails: ValidationProblemDetails hereda de
        //    ProblemDetails y, sin la clave, se deserializa también
        //    correctamente como base, dejando Errors vacío. Necesitamos la
        //    inspección del JSON crudo para distinguir los dos shapes).
        if (TryReadValidationErrors(body, out var validation))
        {
            // "errors" presente en el cuerpo = shape Validation. Devolvemos
            // un diccionario (vacío si Errors.Count == 0) para que la
            // caller pueda distinguir "shape Validation, sin per-field" de
            // "shape ProblemDetails plano" (donde fieldErrors será null).
            IReadOnlyDictionary<string, string[]> fieldErrors =
                new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (validation!.Errors is { Count: > 0 })
            {
                var copy = new Dictionary<string, string[]>(validation.Errors.Count, StringComparer.Ordinal);
                foreach (var kvp in validation.Errors)
                {
                    copy[kvp.Key] = kvp.Value.ToArray();
                }
                fieldErrors = copy;
            }

            return new Result(response.StatusCode, validation.Title, validation.Detail, fieldErrors);
        }

        // 2) Fallback a ProblemDetails plano. Si tampoco parsea, devolvemos
        //    nulls (body era HTML, vacío con whitespace raro, etc.).
        ProblemDetails? problem = null;
        try
        {
            problem = JsonSerializer.Deserialize<ProblemDetails>(body, SerializerOptions);
        }
        catch (JsonException)
        {
        }

        return new Result(response.StatusCode, problem?.Title, problem?.Detail, FieldErrors: null);
    }

    /// <summary>
    /// Lee el cuerpo y devuelve <c>true</c> sólo si la clave <c>errors</c>
    /// está presente y es un objeto JSON (no un valor escalar). En ese
    /// caso, expone el <see cref="ValidationProblemDetails"/> deserializado
    /// vía <paramref name="validation"/>. Si la clave está ausente o el
    /// cuerpo no parsea como ProblemDetails, devuelve <c>false</c>.
    /// </summary>
    private static bool TryReadValidationErrors(string body, out ValidationProblemDetails? validation)
    {
        validation = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("errors", out var errorsElement)
                || errorsElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Parsear explícitamente como ValidationProblemDetails para
            // proyectar Errors. Como ya validamos que "errors" existe,
            // sabemos que estamos ante el shape validation.
            try
            {
                validation = JsonSerializer.Deserialize<ValidationProblemDetails>(body, SerializerOptions);
            }
            catch (JsonException)
            {
                return false;
            }

            return validation is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}