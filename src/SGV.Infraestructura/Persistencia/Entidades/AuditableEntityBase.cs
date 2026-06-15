namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Base de las entidades de persistencia auditables de Infraestructura.
/// Análoga a <c>EntidadAuditable</c> del Dominio.
/// </summary>
public abstract class AuditableEntityBase : EntityBase
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedByUserId { get; set; }
}
