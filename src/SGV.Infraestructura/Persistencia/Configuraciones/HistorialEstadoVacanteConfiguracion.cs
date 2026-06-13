using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Vacantes;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class HistorialEstadoVacanteConfiguracion : IEntityTypeConfiguration<HistorialEstadoVacante>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoVacante> builder)
    {
        builder.ToTable("HistorialEstadosVacante");
        builder.ConfigurarId();
        builder.Property(e => e.ChangedByUserId).HasMaxLength(450);
        builder.Property(e => e.Motivo).HasMaxLength(500);

        builder.HasOne(e => e.Vacante)
            .WithMany(e => e.HistorialEstados)
            .HasForeignKey(e => e.VacanteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EstadoAnterior).WithMany().HasForeignKey(e => e.EstadoAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EstadoNuevo).WithMany().HasForeignKey(e => e.EstadoNuevoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VacanteId, e.ChangedAt });
    }
}
