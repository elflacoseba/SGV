namespace SGV.Dominio.Comun;

/// <summary>
/// Base type for domain entities. Declared as <c>record class</c> so that
/// aggregate roots such as <c>UnidadOrganizativa</c> can use the record
/// + <c>init</c> + <c>with</c> pattern while inheriting an EF Core-friendly
/// <see cref="Id"/> with a public setter.
/// </summary>
public abstract record class EntidadBase
{
    /// <summary>
    /// Mutable identifier. EF Core and the change tracker rely on being able
    /// to set this property; subclass records may opt into <c>init</c>-only
    /// access for their own properties, but the inherited <c>Id</c> remains
    /// <c>public set</c> for compatibility.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
