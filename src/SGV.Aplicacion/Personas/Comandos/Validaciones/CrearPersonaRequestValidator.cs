using System.Text.RegularExpressions;
using FluentValidation;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;

namespace SGV.Aplicacion.Personas.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="CrearPersonaRequest"/>.
/// Issue #147 PR2: además de las reglas de forma, consulta
/// <see cref="ITipoDocumentoCatalogoConsulta"/> para validar
/// <c>FK_INEXISTENTE</c> (Id ausente del catálogo),
/// <c>PATRON_NO_CUMPLIDO</c> (regex no matchea) y
/// <c>LONGITUD_FUERA_DE_RANGO</c> (largo fuera de
/// <c>[LongitudMinima, LongitudMaxima]</c>) contra el tipo seleccionado.
/// </summary>
public class CrearPersonaRequestValidator : AbstractValidator<CrearPersonaRequest>
{
    // Timeout defensivo contra ReDoS — igual al usado por TipoDocumento.ValidarNumeroDocumento.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    public CrearPersonaRequestValidator()
        : this(catalogo: null)
    {
    }

    /// <summary>
    /// Constructor primario: inyección de <see cref="ITipoDocumentoCatalogoConsulta"/>
    /// para validación contra el catálogo. <paramref name="catalogo"/> puede ser
    /// <c>null</c> en escenarios donde la validación contra el catálogo se delega
    /// (back-compat con tests antiguos).
    /// </summary>
    public CrearPersonaRequestValidator(ITipoDocumentoCatalogoConsulta? catalogo)
    {
        // Legajo es opcional: el modelo de dominio (Persona) lo permite
        // null/vacío (ValidacionesDominio.Opcional) y la columna
        // `Personas.Legajo` es nullable. Lo único que aplicamos cuando
        // hay valor es el control de longitud máxima. Esta decisión
        // destrabó también el bootstrap del primer Administrador
        // (SetupSolicitaLegajoOpcional / issue #195 WU-3) sin obligar
        // al usuario final a cargar un legajo del que podría no
        // disponer al primer inicio del sistema.
        RuleFor(x => x.Legajo)
            .MaximumLength(50)
                .When(x => !string.IsNullOrEmpty(x.Legajo));

        RuleFor(x => x.Nombres)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Apellidos)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));

        // Regla 1: FK_INEXISTENTE. El TipoDocumentoId debe existir en el catálogo
        // (cuando está informado y no es Guid.Empty).
        // Se usa el property path `TipoDocumentoId` (no `.Value`) para que el
        // error quede asociado a la propiedad correcta.
        RuleFor(x => x.TipoDocumentoId)
            .MustAsync(async (id, ct) =>
            {
                if (!id.HasValue || id.Value == Guid.Empty)
                {
                    return true;
                }
                if (catalogo is null)
                {
                    return true;
                }
                return await catalogo.ObtenerPorIdAsync(id.Value, ct).ConfigureAwait(false) is not null;
            })
            .WithErrorCode("FK_INEXISTENTE")
            .WithMessage("El tipo de documento seleccionado no existe en el catálogo.")
            .When(x => x.TipoDocumentoId.HasValue && x.TipoDocumentoId != Guid.Empty);

        // Regla 1.b: Guid.Empty es rechazado si el caller lo envía explícitamente
        // (back-compat con el comportamiento pre-PR2). El código de error
        // coincide con el contrato histórico.
        RuleFor(x => x.TipoDocumentoId)
            .NotEqual(Guid.Empty)
            .WithErrorCode("FK_INEXISTENTE")
            .WithMessage("El tipo de documento seleccionado no existe en el catálogo.")
            .When(x => x.TipoDocumentoId.HasValue && x.TipoDocumentoId == Guid.Empty);

        // Regla 2: LONGITUD_FUERA_DE_RANGO. El largo del NumeroDocumento debe estar
        // dentro del rango [LongitudMinima, LongitudMaxima] del tipo seleccionado.
        // Si el TipoDocumentoId no existe en el catálogo, esta regla no aplica
        // (FK_INEXISTENTE ya emite el error correcto).
        RuleFor(x => x.NumeroDocumento)
            .MustAsync(async (request, numero, ct) =>
            {
                if (request.TipoDocumentoId is null || request.TipoDocumentoId == Guid.Empty)
                {
                    return true;
                }
                if (string.IsNullOrEmpty(numero) || catalogo is null)
                {
                    return true;
                }
                var tipo = await catalogo.ObtenerPorIdAsync(request.TipoDocumentoId.Value, ct).ConfigureAwait(false);
                if (tipo is null)
                {
                    return true; // FK_INEXISTENTE ya cubre este caso
                }
                var trimmed = numero.Trim();
                if (tipo.LongitudMinima.HasValue && trimmed.Length < tipo.LongitudMinima.Value)
                {
                    return false;
                }
                if (tipo.LongitudMaxima.HasValue && trimmed.Length > tipo.LongitudMaxima.Value)
                {
                    return false;
                }
                return true;
            })
            .WithErrorCode("LONGITUD_FUERA_DE_RANGO")
            .WithMessage("La longitud del número de documento está fuera del rango permitido por el tipo seleccionado.")
            .When(x => x.TipoDocumentoId.HasValue && x.TipoDocumentoId != Guid.Empty && !string.IsNullOrEmpty(x.NumeroDocumento));

        // Regla 3: PATRON_NO_CUMPLIDO. El NumeroDocumento debe matchear el PatronValidacion
        // del tipo seleccionado. Si el TipoDocumentoId no existe, esta regla no aplica.
        RuleFor(x => x.NumeroDocumento)
            .MustAsync(async (request, numero, ct) =>
            {
                if (request.TipoDocumentoId is null || request.TipoDocumentoId == Guid.Empty)
                {
                    return true;
                }
                if (string.IsNullOrEmpty(numero) || catalogo is null)
                {
                    return true;
                }
                var tipo = await catalogo.ObtenerPorIdAsync(request.TipoDocumentoId.Value, ct).ConfigureAwait(false);
                if (tipo is null)
                {
                    return true;
                }
                if (string.IsNullOrEmpty(tipo.PatronValidacion))
                {
                    return true;
                }
                try
                {
                    return Regex.IsMatch(numero.Trim(), tipo.PatronValidacion, RegexOptions.CultureInvariant, RegexTimeout);
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            })
            .WithErrorCode("PATRON_NO_CUMPLIDO")
            .WithMessage("El número de documento no cumple el patrón del tipo seleccionado.")
            .When(x => x.TipoDocumentoId.HasValue && x.TipoDocumentoId != Guid.Empty && !string.IsNullOrEmpty(x.NumeroDocumento));

        RuleFor(x => x.NumeroDocumento)
            .MaximumLength(50);

        RuleFor(x => x.Telefono)
            .MaximumLength(50);
    }
}
