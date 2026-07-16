using System.Text.Json;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Seguridad;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia;

/// <summary>
/// Persists explicit audit records for Identity mutations, which are not handled by
/// <see cref="AuditoriaSaveChangesInterceptor"/> because Identity users do not derive
/// from <see cref="EntityBase"/>.
/// </summary>
public sealed class AuditoriaServicio(
    SgvDbContext context,
    IUsuarioActual usuarioActual) : IAuditoriaServicio
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RegistrarAsync(
        string entidad,
        string entityId,
        string accion,
        string? usuarioOperadorId,
        IReadOnlyDictionary<string, object?> valoresAnteriores,
        IReadOnlyDictionary<string, object?> valoresNuevos,
        CancellationToken cancellationToken = default)
    {
        var changedProperties = valoresAnteriores.Keys
            .Union(valoresNuevos.Keys, StringComparer.Ordinal)
            .Where(property => !Equals(
                valoresAnteriores.GetValueOrDefault(property),
                valoresNuevos.GetValueOrDefault(property)))
            .OrderBy(property => property, StringComparer.Ordinal)
            .ToArray();

        context.Auditorias.Add(new AuditoriaEntity
        {
            Id = Guid.NewGuid(),
            UserId = usuarioOperadorId ?? usuarioActual.UserId,
            OccurredAt = DateTime.UtcNow,
            EntityName = entidad,
            EntityId = entityId,
            Operation = accion,
            OldValuesJson = valoresAnteriores.Count == 0
                ? null
                : JsonSerializer.Serialize(valoresAnteriores, JsonOptions),
            NewValuesJson = valoresNuevos.Count == 0
                ? null
                : JsonSerializer.Serialize(valoresNuevos, JsonOptions),
            ChangedPropertiesJson = JsonSerializer.Serialize(changedProperties, JsonOptions),
            CorrelationId = usuarioActual.CorrelationId
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
