namespace SGV.Aplicacion.Common;

/// <summary>
/// Shared utilities for application-layer validation and error handling.
/// </summary>
/// <remarks>
/// <para>
/// Pre-issue-#102 this helper lived under
/// <c>SGV.Aplicacion.Personas.Comandos.Validaciones</c> with
/// <c>internal</c> visibility. Several service classes (Cargo,
/// UnidadOrganizativa, Habilidad, Puesto, Ocupacion) shipped their own
/// private copies of <see cref="ToCamelCase"/> and
/// <see cref="BuildFieldErrors"/>. Centralization moves the helper here
/// and exposes it as <c>public</c> so all write services share one
/// implementation.
/// </para>
/// <para>
/// The contract is intentionally narrow: only string-casing and field-error
/// grouping. Anything that needs to inspect the HTTP layer (status codes,
/// ProblemDetails) lives in <c>SGV.Api.Infrastructure.Results.ApiResults</c>,
/// not here — keeping the application layer free of ASP.NET Core
/// dependencies.
/// </para>
/// </remarks>
public static class ValidationHelper
{
    /// <summary>
    /// Converts a PascalCase property name to camelCase for field-error
    /// keys, matching the JSON casing of incoming API requests.
    /// </summary>
    public static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using
    /// camelCase keys. Returns an empty dictionary when there are no
    /// failures.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}