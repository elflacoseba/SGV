using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class VacanteConfiguracion : IEntityTypeConfiguration<VacanteEntity>
{
    public void Configure(EntityTypeBuilder<VacanteEntity> builder)
    {
        builder.ToTable("Vacantes");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.HasOne(e => e.Puesto)
            .WithMany(e => e.Vacantes)
            .HasForeignKey(e => e.PuestoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EstadoVacante)
            .WithMany()
            .HasForeignKey(e => e.EstadoVacanteId)
            .OnDelete(DeleteBehavior.Restrict);

        // MySQL does not support filtered indexes. Use generated columns that
        // are NULL when the vacante is not active (cerrada or soft-deleted) so
        // the unique index enforces one active vacante per puesto — defense
        // in depth contra la ventana TOCTOU del pre-check
        // ExistsAbiertaByPuestoAsync (issue #238).
        //
        // Note: `HasColumnType("char(36)")` se omite intencionalmente
        // (mismo motivo que en OcupacionConfiguracion.cs:36-41): EF Core 9 +
        // Pomelo 9 lanzan NullReferenceException al combinar HasColumnType +
        // HasComputedColumnSql + string property. Pomelo defaults a
        // varchar(36) cuando solo se setea HasMaxLength; equivalente
        // funcionalmente a char(36) para Guids ASCII de 36 chars fijos.
        builder.Property<string?>("ActivePuestoIdUnique")
            .HasMaxLength(36)
            .UseCollation("ascii_general_ci")
            .HasComputedColumnSql("CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END", stored: true)
            .IsRequired(false);
        builder.HasIndex("ActivePuestoIdUnique").IsUnique().HasDatabaseName("IX_Vacantes_ActivePuestoIdUnique");

        builder.HasIndex(e => e.PuestoId);
        builder.HasIndex(e => e.EstadoVacanteId);
        builder.HasIndex(e => e.FechaApertura);
        builder.HasIndex(e => new { e.EstadoVacanteId, e.FechaApertura });
    }
}
