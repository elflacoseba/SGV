using FluentValidation;
using SGV.Contracts.Organizacion.Comandos;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates the link-level fields of <see cref="AsignarCargoSkillRequest"/>:
/// <list type="bullet">
///   <item><description><c>NivelRequeridoId</c> is required (not <see cref="Guid.Empty"/>).</description></item>
///   <item><description><c>Ponderacion</c> is required; when supplied it MUST be in the
///   range (0, 100] with at most 2 decimal places.</description></item>
///   <item><description><c>EsObligatoria</c> is optional; no validation required.</description></item>
/// </list>
/// Reference rules are documented in
/// <c>openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/specs/cargo-skill-ponderacion-obligatoria/spec.md</c>.
/// </summary>
public class AsignarCargoSkillRequestValidator : AbstractValidator<AsignarCargoSkillRequest>
{
    /// <summary>
    /// Inclusive upper bound for <c>Ponderacion</c>, expressed in the same
    /// units persisted on the CargoHabilidad link.
    /// </summary>
    public const decimal PonderacionMaxima = 100.00m;

    /// <summary>
    /// Maximum number of decimal places accepted for <c>Ponderacion</c>.
    /// </summary>
    public const int PonderacionDecimales = 2;

    public AsignarCargoSkillRequestValidator()
    {
        RuleFor(x => x.NivelRequeridoId)
            .NotEqual(Guid.Empty)
            .WithMessage("El nivel requerido es obligatorio.");

        RuleFor(x => x.Ponderacion)
            .NotNull()
            .WithMessage("La ponderación es obligatoria.");

        RuleFor(x => x.Ponderacion)
            .GreaterThan(0)
            .When(x => x.Ponderacion.HasValue)
            .WithMessage("La ponderación debe ser mayor a cero.");

        RuleFor(x => x.Ponderacion)
            .LessThanOrEqualTo(PonderacionMaxima)
            .When(x => x.Ponderacion.HasValue)
            .WithMessage($"La ponderación no puede superar {PonderacionMaxima:0.00}.");

        RuleFor(x => x.Ponderacion)
            .Must(HasAtMostPonderacionDecimals)
            .When(x => x.Ponderacion.HasValue)
            .WithMessage($"La ponderación admite hasta {PonderacionDecimales} decimales.");
    }

    private static bool HasAtMostPonderacionDecimals(decimal? ponderacion)
    {
        if (!ponderacion.HasValue)
        {
            return true;
        }

        return decimal.Round(ponderacion.Value, PonderacionDecimales) == ponderacion.Value;
    }
}