namespace SGV.Dominio.Comun;

/// <summary>
/// Auditable base type. Declared as <c>record class</c> so that
/// <c>UnidadOrganizativa</c> can use the <c>record</c> + <c>init</c> + <c>with</c>
/// pattern while still inheriting a mutable audit shape.
/// <para>
/// <b>Asymmetric design (intentional):</b> subclasses such as
/// <c>UnidadOrganizativa</c> expose their domain properties as
/// <c>init</c>-only to enforce invariants via the compiler (e.g.,
/// <c>Codigo</c> immutable post-create). The inherited audit fields keep
/// <c>public set</c> because <c>AuditoriaSaveChangesInterceptor</c> and
/// EF Core's change tracker write <c>CreatedAt</c>, <c>UpdatedAt</c>,
/// <c>IsDeleted</c>, etc. directly. A future refactor could split this
/// base into a record-friendly piece and an EF-friendly piece, but until
/// then the asymmetry is documented and tested.
/// </para>
/// </summary>
public abstract record class EntidadAuditable : EntidadBase
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedByUserId { get; set; }
}
