using FluentValidation;

namespace SGV.Aplicacion.Personas.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarPersonaRequest"/>.
/// </summary>
public class ActualizarPersonaRequestValidator : AbstractValidator<ActualizarPersonaRequest>
{
    public ActualizarPersonaRequestValidator()
    {
        RuleFor(x => x.Legajo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombres)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Apellidos)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.TipoDocumento)
            .MaximumLength(50);

        RuleFor(x => x.NumeroDocumento)
            .MaximumLength(50);

        RuleFor(x => x.Telefono)
            .MaximumLength(50);
    }
}
