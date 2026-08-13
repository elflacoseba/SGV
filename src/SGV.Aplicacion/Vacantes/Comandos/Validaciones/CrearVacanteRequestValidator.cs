using FluentValidation;
using SGV.Contracts.Vacantes.Comandos;

namespace SGV.Aplicacion.Vacantes.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearVacanteRequest"/>.
/// The domain enforces <c>Motivo</c> required + ≤500 chars at construction
/// time; the validator mirrors the upper-bound check so 400 Bad Request
/// is returned before the domain throws <see cref="ArgumentException"/>.
/// </summary>
public class CrearVacanteRequestValidator : AbstractValidator<CrearVacanteRequest>
{
    public CrearVacanteRequestValidator()
    {
        RuleFor(x => x.PuestoId)
            .NotEqual(Guid.Empty)
            .WithMessage("El puesto es obligatorio.");

        // Issue #273 (Slice A): NO validamos EstadoVacanteId acá. El campo
        // es opcional y la capa de Aplicación resuelve el estado:
        //   - null o Guid.Empty → busca "Abierta" en el catálogo.
        //   - Guid válido → respeta el ID provisto y verifica EsTerminal.
        // Toda la lógica de catálogo + estado terminal vive en
        // VacanteServicioComandos.CrearAsync.

        RuleFor(x => x.FechaApertura)
            .NotEqual(default(DateTime))
            .WithMessage("La fecha de apertura es obligatoria.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("El motivo es obligatorio.")
            .MaximumLength(500)
            .WithMessage("El motivo no puede superar 500 caracteres.");

        RuleFor(x => x.Observaciones)
            .MaximumLength(500)
            .WithMessage("Las observaciones no pueden superar 500 caracteres.");
    }
}