using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class AuditoriaConfiguracion : IEntityTypeConfiguration<AuditoriaEntity>
{
    public void Configure(EntityTypeBuilder<AuditoriaEntity> builder)
    {
        builder.ToTable("Auditorias");
        builder.ConfigurarId();

        builder.Property(e => e.UserId).HasMaxLength(450);
        builder.Property(e => e.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Operation).HasMaxLength(50).IsRequired();
        builder.Property(e => e.OldValuesJson).HasColumnType("longtext");
        builder.Property(e => e.NewValuesJson).HasColumnType("longtext");
        builder.Property(e => e.ChangedPropertiesJson).HasColumnType("longtext");

        builder.HasIndex(e => new { e.EntityName, e.EntityId, e.OccurredAt });
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
        builder.HasIndex(e => e.CorrelationId);
        // Índice compuesto covering (CorrelationId, OccurredAt) para
        // sostener el filtro por CorrelationId + orden por OccurredAt
        // (sort=correlacion_desc de la spec auditoria-sort) sin
        // filesort. La columna CorrelationId es nullable; el índice
        // acepta NULL en su leading column y EF Core lo traduce a un
        // índice BTREE estándar de MySQL.
        builder.HasIndex(e => new { e.CorrelationId, e.OccurredAt });
    }
}
