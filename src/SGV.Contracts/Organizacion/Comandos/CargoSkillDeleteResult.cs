using System.Net;

namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Typed result of a CargoSkill delete operation consumed by the web shell.
/// Mirrors the shape of <c>CargoDeleteResult</c> but scoped to the
/// <c>CargoHabilidad</c> subresource, so callers can render success or
/// recoverable failure messages without exposing stack traces.
///
/// The HTTP side returns <c>204 No Content</c> on success and
/// <c>404 Not Found</c> when the association is missing; any other status
/// maps to a non-success result carrying the upstream <c>ProblemDetails</c>
/// title/detail when available. The shape intentionally stays a sibling of
/// <c>CargoDeleteResult</c> to keep the integration seam consistent.
/// </summary>
public sealed record CargoSkillDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message);
