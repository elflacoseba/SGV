using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Auditoria;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class AuditoriaConfiguracion : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
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
    }
}
