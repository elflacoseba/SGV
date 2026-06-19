using FluentValidation;

namespace SGV.Aplicacion.Habilidades.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarHabilidadRequest"/>.
/// Note: Codigo is not present in the update request — it is immutable after creation.
/// </summary>
public class ActualizarHabilidadRequestValidator : AbstractValidator<ActualizarHabilidadRequest>
{
    public ActualizarHabilidadRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Categoria)
            .MaximumLength(100);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);
    }
}
