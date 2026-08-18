using FluentValidation;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarHabilidadRequest"/>.
/// Uniqueness of <c>Codigo</c> against other active Habilidades is enforced by
/// the application service and the database index, not here.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// el campo legacy <c>string? Categoria</c> se reemplaza por
/// <c>Guid? CategoriaId</c>; la validación de catálogo la hace el servicio.
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
            .MaximumLength(HabilidadRules.NombreMaxLength);

        RuleFor(x => x.CategoriaId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoriaId.HasValue)
            .WithName("CategoriaId");

        RuleFor(x => x.Descripcion)
            .MaximumLength(HabilidadRules.DescripcionMaxLength);
    }
}