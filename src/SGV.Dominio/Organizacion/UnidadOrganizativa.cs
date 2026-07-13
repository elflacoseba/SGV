using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

/// <summary>
/// Aggregate root for the organizational unit hierarchy.
/// <para>
/// <see cref="Codigo"/> is part of the unit's logical identity and is assigned
/// exclusively at construction time. Post-create mutations
/// (<see cref="Actualizar"/>, <see cref="DefinirVigencia"/>,
/// <see cref="CambiarUnidadPadre"/>, <see cref="Activar"/>,
/// <see cref="Desactivar"/>) mutate the same instance via <c>private set</c>
/// and never expose <see cref="Codigo"/> as a parameter. This invariant is
/// enforced because the record's properties are <c>private set</c>.
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

    private UnidadOrganizativa()
    {
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public Guid? UnidadPadreId { get; private set; }

    public UnidadOrganizativa? UnidadPadre { get; private set; }

    public Guid TipoUnidadOrganizativaId { get; private set; }

    public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; private set; }

    public string? Descripcion { get; private set; }

    public DateOnly? VigenteDesde { get; private set; }

    public DateOnly? VigenteHasta { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<UnidadOrganizativa> UnidadesHijas => _unidadesHijas;

    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    /// <summary>
    /// Updates the editable fields of the unit. <see cref="Codigo"/> is NOT modified.
    /// Returns void; mutates the same instance via <c>private set</c>.
    /// </summary>
    public void Actualizar(
        string nombre,
        string? descripcion,
        Guid tipoUnidadOrganizativaId,
        Guid? unidadPadreId,
        DateOnly? vigenteDesde,
        DateOnly? vigenteHasta)
    {
        ValidarVigencia(vigenteDesde, vigenteHasta);

        if (tipoUnidadOrganizativaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El tipo de unidad organizativa es obligatorio.",
                nameof(TipoUnidadOrganizativaId));
        }

        if (unidadPadreId == Id)
        {
            throw new InvalidOperationException(
                "Una unidad organizativa no puede ser padre de sí misma.");
        }

        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        TipoUnidadOrganizativaId = tipoUnidadOrganizativaId;
        UnidadPadreId = unidadPadreId;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }

    /// <summary>
    /// Defines the validity window of the unit. Returns void.
    /// </summary>
    public void DefinirVigencia(DateOnly? desde, DateOnly? hasta)
    {
        ValidarVigencia(desde, hasta);
        VigenteDesde = desde;
        VigenteHasta = hasta;
    }

    /// <summary>
    /// Reassigns the parent of the unit. Returns void.
    /// Throws if <paramref name="unidadPadreId"/> points to the same unit.
    /// </summary>
    public void CambiarUnidadPadre(Guid? unidadPadreId)
    {
        if (unidadPadreId == Id)
        {
            throw new InvalidOperationException(
                "Una unidad organizativa no puede ser padre de sí misma.");
        }

        UnidadPadreId = unidadPadreId;
    }

    /// <summary>
    /// Marks the unit as active. Returns void.
    /// </summary>
    public void Activar() => IsActive = true;

    /// <summary>
    /// Marks the unit as inactive (soft delete). Returns void.
    /// </summary>
    public void Desactivar() => IsActive = false;

    private static void ValidarVigencia(DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue && hasta.HasValue && hasta.Value < desde.Value)
        {
            throw new InvalidOperationException(
                "La fecha de fin de vigencia no puede ser anterior al inicio.");
        }
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Asigna todos los
    /// campos persistibles (incluyendo audit + <see cref="IsActive"/> y las nav
    /// <see cref="UnidadPadre"/> + <see cref="TipoUnidadOrganizativa"/>) con
    /// setters tipados para evitar el path de reflexión que
    /// <c>PersistenceToDomainMapper.SetProperty</c> implementaba.
    /// </summary>
    /// <remarks>
    /// Orden canónico: id + audit + <c>IsDeleted</c> primero, luego datos
    /// primarios (Codigo, Nombre, TipoUnidadOrganizativaId, Descripcion,
    /// UnidadPadreId, VigenteDesde, VigenteHasta, IsActive), y por último las
    /// nav properties.
    /// </remarks>
    internal static UnidadOrganizativa Reconstitute(
        Guid id,
        string codigo,
        string nombre,
        Guid tipoUnidadOrganizativaId,
        string? descripcion,
        Guid? unidadPadreId,
        DateOnly? vigenteDesde,
        DateOnly? vigenteHasta,
        bool isActive,
        UnidadOrganizativa? unidadPadre,
        TipoUnidadOrganizativa? tipoUnidadOrganizativa,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        if (tipoUnidadOrganizativaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El tipo de unidad organizativa es obligatorio.",
                nameof(TipoUnidadOrganizativaId));
        }

        ValidarVigencia(vigenteDesde, vigenteHasta);

        var self = new UnidadOrganizativa
        {
            Id = id,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId,
            UpdatedAt = updatedAt,
            UpdatedByUserId = updatedByUserId,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedByUserId = deletedByUserId
        };

        self.Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        self.Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        self.TipoUnidadOrganizativaId = tipoUnidadOrganizativaId;
        self.Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        self.UnidadPadreId = unidadPadreId;
        self.VigenteDesde = vigenteDesde;
        self.VigenteHasta = vigenteHasta;
        self.IsActive = isActive;
        self.UnidadPadre = unidadPadre;
        self.TipoUnidadOrganizativa = tipoUnidadOrganizativa;

        return self;
    }
}