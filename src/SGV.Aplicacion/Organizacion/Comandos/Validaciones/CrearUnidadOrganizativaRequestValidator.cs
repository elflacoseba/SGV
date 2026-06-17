using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearUnidadOrganizativaRequest"/>.
/// </summary>
public class CrearUnidadOrganizativaRequestValidator : AbstractValidator<CrearUnidadOrganizativaRequest>
{
    public CrearUnidadOrganizativaRequestValidator()
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
