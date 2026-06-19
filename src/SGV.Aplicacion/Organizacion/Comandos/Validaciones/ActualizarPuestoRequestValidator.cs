using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarPuestoRequest"/>.
/// Note: Codigo is not present in the update request — it is immutable after creation.
/// </summary>
public class ActualizarPuestoRequestValidator : AbstractValidator<ActualizarPuestoRequest>
{
    public ActualizarPuestoRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);
    }
}
