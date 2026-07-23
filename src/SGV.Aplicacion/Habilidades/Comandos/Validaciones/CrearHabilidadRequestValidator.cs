using FluentValidation;
using SGV.Contracts.Habilidades.Comandos;

namespace SGV.Aplicacion.Habilidades.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearHabilidadRequest"/>.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// el campo legacy <c>string? Categoria</c> se reemplaza por
/// <c>Guid? CategoriaId</c>. La validación contra el catálogo la hace el
/// servicio de aplicación (<c>ExistsCategoriaAsync</c>); acá solo validamos
/// shape (Guid no-vacío cuando se informa).
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

        // CategoriaId opcional por shape; si se informa, NO debe ser Guid.Empty.
        RuleFor(x => x.CategoriaId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoriaId.HasValue)
            .WithName("CategoriaId");

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000);
    }
}