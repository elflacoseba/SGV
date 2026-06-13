using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Ocupaciones;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class OcupacionConfiguracion : IEntityTypeConfiguration<Ocupacion>
{
    public void Configure(EntityTypeBuilder<Ocupacion> builder)
    {
        builder.ToTable("Ocupaciones", table =>
            table.HasCheckConstraint("CK_Ocupaciones_Fechas", "[FechaFin] IS NULL OR [FechaFin] >= [FechaInicio]"));
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

        builder.HasIndex(e => e.PuestoId).IsUnique().HasFilter("[FechaFin] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.PersonaId).IsUnique().HasFilter("[FechaFin] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.PuestoId, e.FechaInicio, e.FechaFin });
        builder.HasIndex(e => new { e.PersonaId, e.FechaInicio, e.FechaFin });
    }
}
