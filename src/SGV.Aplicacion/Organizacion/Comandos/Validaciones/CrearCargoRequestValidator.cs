using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearCargoRequest"/>.
/// </summary>
public class CrearCargoRequestValidator : AbstractValidator<CrearCargoRequest>
{
    public CrearCargoRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);

        RuleFor(x => x.NivelId)
            .NotEqual(Guid.Empty);
    }
}
