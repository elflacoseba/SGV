namespace SGV.Aplicacion.Personas.Comandos.Validaciones;

/// <summary>
/// Shared utilities for validation and error handling in Persona commands.
/// </summary>
internal static class ValidationHelper
{
    /// <summary>
    /// Converts a PascalCase property name to camelCase for field-error keys,
    /// matching the JSON casing of incoming API requests.
    /// </summary>
    public static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using camelCase keys.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
