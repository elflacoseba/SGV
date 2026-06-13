using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Seleccion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class HistorialEstadoPostulacionConfiguracion : IEntityTypeConfiguration<HistorialEstadoPostulacion>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoPostulacion> builder)
    {
        builder.ToTable("HistorialEstadosPostulacion");
        builder.ConfigurarId();
        builder.Property(e => e.ChangedByUserId).HasMaxLength(450);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.HasOne(e => e.Postulacion)
            .WithMany(e => e.HistorialEstados)
            .HasForeignKey(e => e.PostulacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EstadoAnterior).WithMany().HasForeignKey(e => e.EstadoAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EstadoNuevo).WithMany().HasForeignKey(e => e.EstadoNuevoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.PostulacionId, e.ChangedAt });
    }
}
