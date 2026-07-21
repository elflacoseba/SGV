using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PersonaConfiguracion : IEntityTypeConfiguration<PersonaEntity>
{
    public void Configure(EntityTypeBuilder<PersonaEntity> builder)
    {
        builder.ToTable("Personas");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Legajo).HasMaxLength(50);
        builder.Property(e => e.Nombres).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Apellidos).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.TipoDocumentoId)
            .HasColumnType("char(36)")
            .UseCollation("ascii_general_ci");
        builder.Property(e => e.NumeroDocumento).HasMaxLength(50);
        builder.Property(e => e.Telefono).HasMaxLength(50);

        // MySQL does not support filtered indexes. Use generated columns that
        // are NULL when the value is absent or the record is soft-deleted.
        builder.Property<string?>("ActiveLegajoUnique")
            .HasComputedColumnSql("CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveLegajoUnique").IsUnique();

        builder.Property<string?>("ActiveEmailUnique")
            .HasComputedColumnSql("CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveEmailUnique").IsUnique();

        // Issue #147: ActiveDocumentoUnique redefinido con la fórmula
        // CONCAT(TipoDocumentoId, ':', NumeroDocumento). Precedente:
        // FixActivePuestoIdUniqueType (drop-index → alter → create-index por
        // la regla MySQL InnoDB sobre columnas generadas indexadas).
        builder.Property<string?>("ActiveDocumentoUnique")
            .HasColumnType("varchar(120)")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComputedColumnSql("CASE WHEN `TipoDocumentoId` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumentoId`, ':', `NumeroDocumento`) ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveDocumentoUnique").IsUnique();

        builder.HasIndex(e => new { e.Apellidos, e.Nombres });

        builder.HasIndex(e => e.NumeroDocumento)
            .HasDatabaseName("IX_Personas_NumeroDocumento");

        // Issue #147: FK hacia TiposDocumento.Id, OnDelete(Restrict).
        builder.HasOne(e => e.TipoDocumento)
            .WithMany()
            .HasForeignKey(e => e.TipoDocumentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
