namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Auditoria.
/// </summary>
public sealed class AuditoriaEntity : EntityBase
{
    public string? UserId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? ChangedPropertiesJson { get; set; }

    public Guid? CorrelationId { get; set; }
}
