using FluentValidation;

namespace SGV.Aplicacion.Organizacion.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarUnidadOrganizativaRequest"/>.
/// <para>
/// <c>Codigo</c> is NOT validated here because it is not part of the update contract;
/// the unit's <c>Codigo</c> is set once at create time and is immutable thereafter.
/// </para>
/// </summary>
public class ActualizarUnidadOrganizativaRequestValidator : AbstractValidator<ActualizarUnidadOrganizativaRequest>
{
    public ActualizarUnidadOrganizativaRequestValidator()
    {
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
