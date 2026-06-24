using FluentValidation;

namespace SGV.Aplicacion.Ocupaciones.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarOcupacionRequest"/>.
/// </summary>
public class ActualizarOcupacionRequestValidator : AbstractValidator<ActualizarOcupacionRequest>
{
    public ActualizarOcupacionRequestValidator()
    {
        RuleFor(x => x.PersonaId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.PuestoId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.FechaInicio)
            .NotEmpty();

        RuleFor(x => x.TipoAsignacion)
            .IsInEnum();

        RuleFor(x => x.Observaciones)
            .MaximumLength(1000);
    }
}
