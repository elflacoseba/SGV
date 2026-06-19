using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearPuestoRequest"/>.
/// </summary>
public class CrearPuestoRequestValidator : AbstractValidator<CrearPuestoRequest>
{
    public CrearPuestoRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);

        RuleFor(x => x.UnidadOrganizativaId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.CargoId)
            .NotEqual(Guid.Empty);
    }
}
