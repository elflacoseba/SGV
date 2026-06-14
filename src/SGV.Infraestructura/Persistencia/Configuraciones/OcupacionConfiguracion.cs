using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Ocupaciones;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class OcupacionConfiguracion : IEntityTypeConfiguration<Ocupacion>
{
    public void Configure(EntityTypeBuilder<Ocupacion> builder)
    {
        builder.ToTable("Ocupaciones", table =>
            table.HasCheckConstraint("CK_Ocupaciones_Fechas", "`FechaFin` IS NULL OR `FechaFin` >= `FechaInicio`"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.TipoAsignacion).HasMaxLength(50).IsRequired();
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
        // the unique index enforces one active ocupacion per puesto/persona.
        builder.Property<int?>("ActivePuestoIdUnique")
            .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActivePuestoIdUnique").IsUnique();

        builder.Property<int?>("ActivePersonaIdUnique")
            .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActivePersonaIdUnique").IsUnique();

        builder.HasIndex(e => new { e.PuestoId, e.FechaInicio, e.FechaFin });
        builder.HasIndex(e => new { e.PersonaId, e.FechaInicio, e.FechaFin });
    }
}
