using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Typed result of a Persona delete (soft-delete) operation consumed by the web
/// shell. Mirrors <c>CargoDeleteResult</c> so the integration client renders
/// success or recoverable failure without exposing stack traces.
/// </summary>
/// <remarks>
/// The HTTP side returns <c>204 No Content</c> on success and <c>404 Not Found</c>
/// when the persona is missing. Any other status maps to a non-success result
/// carrying the upstream <c>ProblemDetails</c> title/detail when available.
/// </remarks>
public sealed record PersonaDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message,
    ErrorCategoria Categoria = ErrorCategoria.NotFound);