using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PostulacionConfiguracion : IEntityTypeConfiguration<PostulacionEntity>
{
    public void Configure(EntityTypeBuilder<PostulacionEntity> builder)
    {
        builder.ToTable("Postulaciones", table =>
            table.HasCheckConstraint("CK_Postulaciones_PuntajeCompatibilidad", "`PuntajeCompatibilidad` IS NULL OR (`PuntajeCompatibilidad` >= 0 AND `PuntajeCompatibilidad` <= 100)"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.PuntajeCompatibilidad).HasPrecision(5, 2);
        builder.Property(e => e.NivelCompatibilidad).HasMaxLength(50);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.HasOne(e => e.Vacante)
            .WithMany(e => e.Postulaciones)
            .HasForeignKey(e => e.VacanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Postulante)
            .WithMany(e => e.Postulaciones)
            .HasForeignKey(e => e.PostulanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EstadoPostulacion)
            .WithMany()
            .HasForeignKey(e => e.EstadoPostulacionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.VacanteId, e.PostulanteId }).IsUnique();
        builder.HasIndex(e => e.EstadoPostulacionId);
        builder.HasIndex(e => new { e.VacanteId, e.EstadoPostulacionId });
    }
}
