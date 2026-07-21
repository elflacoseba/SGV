using SGV.Dominio.Comun;

namespace SGV.Dominio.Personas;

/// <summary>
/// Read-only catalog entity that classifies a <see cref="Persona"/>'s
/// <c>NumeroDocumento</c>. Immutable at runtime: the catalog is seeded
/// exclusively by an EF Core migration (see <c>TipoDocumentoConstantes</c>).
/// No CRUD endpoints are exposed; any new type requires a new migration.
/// </summary>
public sealed record class TipoDocumento : EntidadBase
{
    private TipoDocumento()
    {
    }

    public TipoDocumento(
        string codigo,
        string nombre,
        string? patronValidacion = null,
        int? longitudMinima = null,
        int? longitudMaxima = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);

        if (longitudMinima is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LongitudMinima),
                "La longitud mínima no puede ser negativa.");
        }

        if (longitudMaxima is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LongitudMaxima),
                "La longitud máxima no puede ser negativa.");
        }

        if (longitudMinima.HasValue
            && longitudMaxima.HasValue
            && longitudMinima.Value > longitudMaxima.Value)
        {
            throw new ArgumentException(
                "La longitud mínima no puede ser mayor que la longitud máxima.",
                nameof(LongitudMinima));
        }

        PatronValidacion = string.IsNullOrWhiteSpace(patronValidacion)
            ? null
            : patronValidacion.Trim();
        LongitudMinima = longitudMinima;
        LongitudMaxima = longitudMaxima;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? PatronValidacion { get; private set; }

    public int? LongitudMinima { get; private set; }

    public int? LongitudMaxima { get; private set; }

    /// <summary>
    /// Validates <paramref name="numeroDocumento"/> against this
    /// <see cref="TipoDocumento"/>'s pattern and length constraints.
    /// Returns <c>true</c> when the value is acceptable.
    /// </summary>
    /// <remarks>
    /// Returns <c>true</c> when <paramref name="numeroDocumento"/> is null or
    /// empty (callers should already have decided whether the document is
    /// mandatory before reaching this method).
    /// </remarks>
    public bool ValidarNumeroDocumento(string? numeroDocumento)
    {
        if (string.IsNullOrWhiteSpace(numeroDocumento))
        {
            return true;
        }

        var trimmed = numeroDocumento.Trim();

        if (LongitudMinima.HasValue && trimmed.Length < LongitudMinima.Value)
        {
            return false;
        }

        if (LongitudMaxima.HasValue && trimmed.Length > LongitudMaxima.Value)
        {
            return false;
        }

        if (string.IsNullOrEmpty(PatronValidacion))
        {
            return true;
        }

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                trimmed,
                PatronValidacion,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50));
        }
        catch (ArgumentException)
        {
            // Patrón inválido en el catalog: tratar como no-match.
            return false;
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
