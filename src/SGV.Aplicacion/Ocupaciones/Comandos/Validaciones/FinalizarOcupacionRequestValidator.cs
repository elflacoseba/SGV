using FluentValidation;

namespace SGV.Aplicacion.Ocupaciones.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="FinalizarOcupacionRequest"/>.
/// </summary>
public class FinalizarOcupacionRequestValidator : AbstractValidator<FinalizarOcupacionRequest>
{
    public FinalizarOcupacionRequestValidator()
    {
        RuleFor(x => x.FechaFin)
            .NotEmpty();

        RuleFor(x => x.Observaciones)
            .MaximumLength(1000);
    }
}
