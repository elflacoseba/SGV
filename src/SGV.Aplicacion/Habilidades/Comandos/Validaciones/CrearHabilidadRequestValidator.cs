using FluentValidation;

namespace SGV.Aplicacion.Habilidades.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearHabilidadRequest"/>.
/// </summary>
public class CrearHabilidadRequestValidator : AbstractValidator<CrearHabilidadRequest>
{
    public CrearHabilidadRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Categoria)
            .MaximumLength(100);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);
    }
}
