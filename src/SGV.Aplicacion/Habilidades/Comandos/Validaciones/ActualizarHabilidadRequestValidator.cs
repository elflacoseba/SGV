using FluentValidation;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarHabilidadRequest"/>.
/// Uniqueness of <c>Codigo</c> against other active Habilidades is enforced by
/// the application service and the database index, not here.
/// </summary>
public class ActualizarHabilidadRequestValidator : AbstractValidator<ActualizarHabilidadRequest>
{
    public ActualizarHabilidadRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(HabilidadRules.CodigoMaxLength);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Categoria)
            .MaximumLength(100);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);
    }
}
