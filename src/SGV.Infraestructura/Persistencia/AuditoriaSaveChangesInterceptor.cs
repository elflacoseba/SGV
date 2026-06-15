using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SGV.Aplicacion.Seguridad;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia;

public sealed class AuditoriaSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUsuarioActual _usuarioActual;

    public AuditoriaSaveChangesInterceptor(IUsuarioActual usuarioActual)
    {
        _usuarioActual = usuarioActual;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AgregarAuditorias(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AgregarAuditorias(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AgregarAuditorias(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var ahora = DateTime.UtcNow;
        var entradas = context.ChangeTracker.Entries()
            .Where(e => e.Entity is EntityBase and not AuditoriaEntity)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entrada in entradas)
        {
            AplicarAuditoriaTecnica(entrada, ahora);

            var auditoria = CrearAuditoria(entrada, ahora);
            if (auditoria is not null)
            {
                context.Set<AuditoriaEntity>().Add(auditoria);
            }
        }
    }

    private void AplicarAuditoriaTecnica(EntityEntry entrada, DateTime ahora)
    {
        if (entrada.Entity is not AuditableEntityBase auditable)
        {
            return;
        }

        if (entrada.State == EntityState.Added)
        {
            auditable.CreatedAt = ahora;
            auditable.CreatedByUserId = _usuarioActual.UserId;
            return;
        }

        auditable.UpdatedAt = ahora;
        auditable.UpdatedByUserId = _usuarioActual.UserId;

        if (entrada.State == EntityState.Deleted)
        {
            entrada.State = EntityState.Modified;
            auditable.IsDeleted = true;
            auditable.DeletedAt = ahora;
            auditable.DeletedByUserId = _usuarioActual.UserId;
        }
    }

    private AuditoriaEntity? CrearAuditoria(EntityEntry entrada, DateTime ahora)
    {
        var entidadId = entrada.Property(nameof(EntityBase.Id)).CurrentValue?.ToString();
        if (string.IsNullOrWhiteSpace(entidadId))
        {
            return null;
        }

        var operacion = entrada.State switch
        {
            EntityState.Added => "Alta",
            EntityState.Modified when entrada.Entity is AuditableEntityBase { IsDeleted: true } => "BajaLogica",
            EntityState.Modified => "Modificacion",
            EntityState.Deleted => "BajaLogica",
            _ => "Desconocida"
        };

        var auditoria = new AuditoriaEntity
        {
            Id = Guid.NewGuid(),
            UserId = _usuarioActual.UserId,
            OccurredAt = ahora,
            EntityName = ObtenerNombreLogicoEntidad(entrada.Metadata.ClrType.Name),
            EntityId = entidadId,
            Operation = operacion,
            OldValuesJson = SerializarValores(entrada, usarOriginales: true),
            NewValuesJson = SerializarValores(entrada, usarOriginales: false),
            ChangedPropertiesJson = SerializarPropiedadesCambiadas(entrada),
            CorrelationId = _usuarioActual.CorrelationId
        };

        return auditoria;
    }

    private static string ObtenerNombreLogicoEntidad(string clrTypeName)
    {
        const string entitySuffix = "Entity";

        return clrTypeName.EndsWith(entitySuffix, StringComparison.Ordinal)
            ? clrTypeName[..^entitySuffix.Length]
            : clrTypeName;
    }

    private static string? SerializarValores(EntityEntry entrada, bool usarOriginales)
    {
        if (entrada.State == EntityState.Added && usarOriginales)
        {
            return null;
        }

        if (entrada.State == EntityState.Deleted && !usarOriginales)
        {
            return null;
        }

        var valores = entrada.Properties
            .Where(p => !EsCampoSensible(p.Metadata.Name))
            .ToDictionary(
                p => p.Metadata.Name,
                p => usarOriginales ? p.OriginalValue : p.CurrentValue);

        return JsonSerializer.Serialize(valores, JsonOptions);
    }

    private static string SerializarPropiedadesCambiadas(EntityEntry entrada)
    {
        var propiedades = entrada.Properties
            .Where(p => p.IsModified)
            .Where(p => !EsCampoSensible(p.Metadata.Name))
            .Select(p => p.Metadata.Name)
            .ToArray();

        return JsonSerializer.Serialize(propiedades, JsonOptions);
    }

    private static bool EsCampoSensible(string nombre)
    {
        return nombre.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("ConcurrencyStamp", StringComparison.OrdinalIgnoreCase);
    }
}
