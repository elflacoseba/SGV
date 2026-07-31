using FluentValidation;
using SGV.Contracts.Vacantes.Comandos;

namespace SGV.Aplicacion.Vacantes.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CambiarEstadoVacanteRequest"/>.
/// PB-3 confirmado: <c>Motivo</c> es opcional al cerrar (no se valida).
/// El validador sí exige <c>EstadoVacanteId</c> no vacío y
/// <c>Observaciones</c> ≤500 chars para mantener la paridad con el
/// resto de los wire-types del módulo Vacantes.
/// </summary>
public class CambiarEstadoVacanteRequestValidator : AbstractValidator<CambiarEstadoVacanteRequest>
{
    public CambiarEstadoVacanteRequestValidator()
    {
        RuleFor(x => x.EstadoVacanteId)
            .NotEqual(Guid.Empty)
            .WithMessage("El estado de la vacante es obligatorio.");

        RuleFor(x => x.Observaciones)
            .MaximumLength(500)
            .WithMessage("Las observaciones no pueden superar 500 caracteres.");
    }
}