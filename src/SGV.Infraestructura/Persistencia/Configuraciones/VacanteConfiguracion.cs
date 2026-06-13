using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Vacantes;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class VacanteConfiguracion : IEntityTypeConfiguration<Vacante>
{
    public void Configure(EntityTypeBuilder<Vacante> builder)
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

        builder.HasIndex(e => e.PuestoId);
        builder.HasIndex(e => e.EstadoVacanteId);
        builder.HasIndex(e => e.FechaApertura);
        builder.HasIndex(e => new { e.EstadoVacanteId, e.FechaApertura }).HasFilter("[FechaCierre] IS NULL");
    }
}
