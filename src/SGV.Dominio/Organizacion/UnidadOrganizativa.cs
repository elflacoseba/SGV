using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

/// <summary>
/// Aggregate root for the organizational unit hierarchy.
/// <para>
/// <see cref="Codigo"/> is part of the unit's logical identity and is assigned
/// exclusively at construction time. Post-create mutations (<see cref="Actualizar"/>,
/// <see cref="DefinirVigencia"/>, <see cref="CambiarUnidadPadre"/>, <see cref="Activar"/>,
/// <see cref="Desactivar"/>) return a new instance via <c>with</c> and never expose
/// <see cref="Codigo"/> as a parameter. This invariant is enforced by the compiler
/// because the record's properties are <c>init</c>-only.
/// </para>
/// </summary>
public sealed record class UnidadOrganizativa : EntidadAuditable
{
    private readonly List<UnidadOrganizativa> _unidadesHijas = [];
    private readonly List<Puesto> _puestos = [];

    public UnidadOrganizativa(
        string codigo,
        string nombre,
        Guid tipoUnidadOrganizativaId,
        string? descripcion = null,
        Guid? unidadPadreId = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        if (tipoUnidadOrganizativaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El tipo de unidad organizativa es obligatorio.",
                nameof(TipoUnidadOrganizativaId));
        }

        TipoUnidadOrganizativaId = tipoUnidadOrganizativaId;
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        UnidadPadreId = unidadPadreId;
        IsActive = true;
    }

    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public Guid? UnidadPadreId { get; init; }

    public UnidadOrganizativa? UnidadPadre { get; init; }

    public Guid TipoUnidadOrganizativaId { get; init; }

    public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; init; }

    public string? Descripcion { get; init; }

    public DateOnly? VigenteDesde { get; init; }

    public DateOnly? VigenteHasta { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyCollection<UnidadOrganizativa> UnidadesHijas => _unidadesHijas;

    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    /// <summary>
    /// Updates the editable fields of the unit. <see cref="Codigo"/> is NOT modified.
    /// Returns a new record instance; the original is untouched.
    /// </summary>
    public UnidadOrganizativa Actualizar(
        string nombre,
        string? descripcion,
        Guid tipoUnidadOrganizativaId,
        Guid? unidadPadreId,
        DateOnly? vigenteDesde,
        DateOnly? vigenteHasta)
    {
        ValidarVigencia(vigenteDesde, vigenteHasta);

        var candidato = this with
        {
            Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200),
            Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000),
            TipoUnidadOrganizativaId = tipoUnidadOrganizativaId == Guid.Empty
                ? throw new ArgumentException(
                    "El tipo de unidad organizativa es obligatorio.",
                    nameof(TipoUnidadOrganizativaId))
                : tipoUnidadOrganizativaId,
            UnidadPadreId = unidadPadreId,
            VigenteDesde = vigenteDesde,
            VigenteHasta = vigenteHasta
        };

        if (unidadPadreId == candidato.Id)
        {
            throw new InvalidOperationException(
                "Una unidad organizativa no puede ser padre de sí misma.");
        }

        return candidato;
    }

    /// <summary>
    /// Defines the validity window of the unit. Returns a new record instance.
    /// </summary>
    public UnidadOrganizativa DefinirVigencia(DateOnly? desde, DateOnly? hasta)
    {
        ValidarVigencia(desde, hasta);
        return this with { VigenteDesde = desde, VigenteHasta = hasta };
    }

    /// <summary>
    /// Reassigns the parent of the unit. Returns a new record instance.
    /// Throws if <paramref name="unidadPadreId"/> points to the same unit.
    /// </summary>
    public UnidadOrganizativa CambiarUnidadPadre(Guid? unidadPadreId)
    {
        if (unidadPadreId == Id)
        {
            throw new InvalidOperationException(
                "Una unidad organizativa no puede ser padre de sí misma.");
        }

        return this with { UnidadPadreId = unidadPadreId };
    }

    /// <summary>
    /// Marks the unit as active. Returns a new record instance.
    /// </summary>
    public UnidadOrganizativa Activar() => this with { IsActive = true };

    /// <summary>
    /// Marks the unit as inactive (soft delete). Returns a new record instance.
    /// </summary>
    public UnidadOrganizativa Desactivar() => this with { IsActive = false };

    private static void ValidarVigencia(DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue && hasta.HasValue && hasta.Value < desde.Value)
        {
            throw new InvalidOperationException(
                "La fecha de fin de vigencia no puede ser anterior al inicio.");
        }
    }
}
