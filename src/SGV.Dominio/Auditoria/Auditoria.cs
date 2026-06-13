using SGV.Dominio.Comun;

namespace SGV.Dominio.Auditoria;

public sealed class Auditoria : EntidadBase
{
    private Auditoria()
    {
    }

    public Auditoria(string? userId, DateTime occurredAt, string entityName, string entityId, string operation)
    {
        UserId = ValidacionesDominio.Opcional(userId, nameof(UserId), 450);
        OccurredAt = occurredAt;
        EntityName = ValidacionesDominio.Requerido(entityName, nameof(EntityName), 200);
        EntityId = ValidacionesDominio.Requerido(entityId, nameof(EntityId), 100);
        Operation = ValidacionesDominio.Requerido(operation, nameof(Operation), 50);
    }

    public string? UserId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public string EntityName { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string Operation { get; private set; } = string.Empty;

    public string? OldValuesJson { get; private set; }

    public string? NewValuesJson { get; private set; }

    public string? ChangedPropertiesJson { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public void RegistrarValores(string? oldValuesJson, string? newValuesJson, string? changedPropertiesJson, Guid? correlationId)
    {
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        ChangedPropertiesJson = changedPropertiesJson;
        CorrelationId = correlationId;
    }
}
