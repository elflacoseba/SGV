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

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return (false, null);
        }

        return (IsInRange(parsed), parsed);
    }

    public static bool IsInRange(decimal value) => value >= Min && value <= Max;
}
