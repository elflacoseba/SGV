using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarUnidadOrganizativaRequest"/>.
/// </summary>
public class ActualizarUnidadOrganizativaRequestValidator : AbstractValidator<ActualizarUnidadOrganizativaRequest>
{
    public ActualizarUnidadOrganizativaRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);

        RuleFor(x => x.TipoUnidadOrganizativaId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.VigenteHasta)
            .GreaterThanOrEqualTo(x => x.VigenteDesde)
            .When(x => x.VigenteDesde.HasValue && x.VigenteHasta.HasValue);
    }
}
