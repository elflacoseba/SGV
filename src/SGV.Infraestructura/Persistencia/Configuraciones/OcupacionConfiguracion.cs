using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class OcupacionConfiguracion : IEntityTypeConfiguration<OcupacionEntity>
{
    public void Configure(EntityTypeBuilder<OcupacionEntity> builder)
    {
        builder.ToTable("Ocupaciones", table =>
            table.HasCheckConstraint("CK_Ocupaciones_Fechas", "`FechaFin` IS NULL OR `FechaFin` >= `FechaInicio`"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.TipoAsignacion)
            .IsRequired()
            .HasConversion<int>();
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.HasOne(e => e.Persona)
            .WithMany(e => e.Ocupaciones)
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Puesto)
            .WithMany(e => e.Ocupaciones)
            .HasForeignKey(e => e.PuestoId)
            .OnDelete(DeleteBehavior.Restrict);

        // MySQL does not support filtered indexes. Use generated columns that
        // are NULL when the ocupacion is not active (ended or soft-deleted) so
        // the unique index enforces one active ocupacion per puesto,
        // and one active ocupacion per persona+puesto combination.
        //
        // Note: `HasColumnType("char(36)")` is intentionally omitted. EF Core 9 +
        // Pomelo 9 throw NullReferenceException when combining HasColumnType +
        // HasComputedColumnSql + string property. Pomelo defaults to varchar(36)
        // when only HasMaxLength is set; functionally equivalent to char(36)
        // for fixed-width 36-char ASCII Guids. The byte overhead (1 length-prefix
        // byte per row) is acceptable for the blast radius of this column.
        builder.Property<string?>("ActivePuestoIdUnique")
            .HasMaxLength(36)
            .UseCollation("ascii_general_ci")
            .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActivePuestoIdUnique").IsUnique();

        builder.Property<string?>("ActivePersonaPuestoUnique")
            .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END")
            .IsRequired(false)
            .HasMaxLength(100);
        builder.HasIndex("ActivePersonaPuestoUnique").IsUnique();

        builder.HasIndex(e => new { e.PuestoId, e.FechaInicio, e.FechaFin });
        builder.HasIndex(e => new { e.PersonaId, e.FechaInicio, e.FechaFin });
    }
}
