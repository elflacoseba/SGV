using System.Globalization;

namespace SGV.Web.Integration.Organizacion;

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

    /// <summary>
    /// Parses <paramref name="raw"/> using <see cref="CultureInfo.InvariantCulture"/>
    /// and validates it against the <see cref="Min"/>/<see cref="Max"/> range.
    /// Returns the parsed value (or <see langword="null"/> if the string is
    /// blank/unparseable) together with a flag that signals whether the
    /// value also lies inside the range. Callers that bind the value to
    /// the request DTO should propagate <c>Value</c> as-is so the user
    /// still sees the offending input on re-render; callers that gate
    /// the request on validity should check <c>IsValid</c>.
    /// </summary>
    public static (bool IsValid, decimal? Value) TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (false, null);
        }

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return (false, null);
        }

        return (IsInRange(parsed), parsed);
    }

    /// <summary><see langword="true"/> when <paramref name="value"/> lies in [<see cref="Min"/>, <see cref="Max"/>].</summary>
    public static bool IsInRange(decimal value) => value >= Min && value <= Max;
}
