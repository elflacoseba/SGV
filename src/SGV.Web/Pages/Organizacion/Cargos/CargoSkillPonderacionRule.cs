using System.Globalization;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// Single source of truth for the editable Cargo-Habilidad grid Ponderacion
/// field. The rule, its numeric range and its localized error message are
/// referenced from <see cref="CargoSkillFormHelpers"/> for both the
/// "Asignar nueva habilidad" and the per-row "Actualizar" forms so the
/// validation contract is identical for every input that mutates the
/// vínculo <c>CargoHabilidad</c>.
/// </summary>
public static class CargoSkillPonderacionRule
{
    /// <summary>Lower bound (inclusive) of a valid ponderación.</summary>
    public const decimal Min = 0.01m;

    /// <summary>Upper bound (inclusive) of a valid ponderación.</summary>
    public const decimal Max = 100.00m;

    /// <summary>Localized error message anchored to the Ponderación field.</summary>
    public const string ErrorMessage = "La ponderación debe estar entre 0,01 y 100,00.";

    public static (bool IsValid, decimal? Value) TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (false, null);
        }

        // Acepta "1.50" (formato invariante: JSON wire, hábito de tipeo)
        // y "1,50" (es-AR, configuración regional del shell web).
        //
        // Estrategia de parseo: si el string tiene coma y NO tiene punto,
        // la coma es el separador decimal de es-AR y la reemplazamos por
        // punto antes de parsear en InvariantCulture. Esto evita la
        // ambigüedad del parser con NumberStyles.Number en es-AR, donde
        // la coma es a la vez separador de miles y decimal — "100,00"
        // sin contexto se interpreta como 10000, no 100.
        //
        // Si tiene punto, lo dejamos tal cual (formato invariante puro).
        // Si tiene ambos (punto y coma), no tocamos: lo más probable es
        // que sea un input malformado y el parse fallará de cualquier
        // manera.
        var normalized = raw.Contains(',') && !raw.Contains('.')
            ? raw.Replace(',', '.')
            : raw;

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return (IsInRange(parsed), parsed);
        }

        return (false, null);
    }

    public static bool IsInRange(decimal value) => value >= Min && value <= Max;
}
