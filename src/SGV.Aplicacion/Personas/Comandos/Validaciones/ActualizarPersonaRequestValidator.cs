using System.Text.RegularExpressions;
using FluentValidation;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;

namespace SGV.Aplicacion.Personas.Comandos.Validaciones;

/// <summary>
/// Validates shape and input rules for <see cref="ActualizarPersonaRequest"/>.
/// Issue #147 PR2: además de las reglas de forma, consulta
/// <see cref="ITipoDocumentoCatalogoConsulta"/> para validar
/// <c>FK_INEXISTENTE</c>, <c>PATRON_NO_CUMPLIDO</c> y
/// <c>LONGITUD_FUERA_DE_RANGO</c> contra el tipo seleccionado.
/// Réplica del contrato de <see cref="CrearPersonaRequestValidator"/>.
/// </summary>
public class ActualizarPersonaRequestValidator : AbstractValidator<ActualizarPersonaRequest>
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    public ActualizarPersonaRequestValidator()
        : this(catalogo: null)
    {
    }

    public ActualizarPersonaRequestValidator(ITipoDocumentoCatalogoConsulta? catalogo)
    {
        // Misma política que CrearPersonaRequestValidator: Legajo es
        // opcional según el dominio y la columna. Sólo se valida la
        // longitud máxima cuando hay valor presente.
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

        // FK_INEXISTENTE.
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

        // Guid.Empty rechazado explícitamente (back-compat pre-PR2).
        RuleFor(x => x.TipoDocumentoId)
            .NotEqual(Guid.Empty)
            .WithErrorCode("FK_INEXISTENTE")
            .WithMessage("El tipo de documento seleccionado no existe en el catálogo.")
            .When(x => x.TipoDocumentoId.HasValue && x.TipoDocumentoId == Guid.Empty);

        // LONGITUD_FUERA_DE_RANGO.
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

        // PATRON_NO_CUMPLIDO.
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
