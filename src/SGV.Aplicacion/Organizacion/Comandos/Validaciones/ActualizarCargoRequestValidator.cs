using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarCargoRequest"/>.
/// Uniqueness of <c>Codigo</c> against other active Cargos is enforced by
/// the application service and the database index, not here.
/// </summary>
public class ActualizarCargoRequestValidator : AbstractValidator<ActualizarCargoRequest>
{
    public ActualizarCargoRequestValidator()
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
