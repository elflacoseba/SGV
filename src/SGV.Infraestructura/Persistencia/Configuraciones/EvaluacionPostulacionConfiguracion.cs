using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Seleccion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class EvaluacionPostulacionConfiguracion : IEntityTypeConfiguration<EvaluacionPostulacion>
{
    public void Configure(EntityTypeBuilder<EvaluacionPostulacion> builder)
    {
        builder.ToTable("EvaluacionesPostulacion", table =>
        {
            table.HasCheckConstraint("CK_EvaluacionesPostulacion_PuntajeTecnico", "[PuntajeTecnico] IS NULL OR ([PuntajeTecnico] >= 0 AND [PuntajeTecnico] <= 100)");
            table.HasCheckConstraint("CK_EvaluacionesPostulacion_PuntajeEntrevista", "[PuntajeEntrevista] IS NULL OR ([PuntajeEntrevista] >= 0 AND [PuntajeEntrevista] <= 100)");
            table.HasCheckConstraint("CK_EvaluacionesPostulacion_PuntajeCompatibilidad", "[PuntajeCompatibilidad] IS NULL OR ([PuntajeCompatibilidad] >= 0 AND [PuntajeCompatibilidad] <= 100)");
        });
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.EvaluadoByUserId).HasMaxLength(450);
        builder.Property(e => e.PuntajeTecnico).HasPrecision(5, 2);
        builder.Property(e => e.PuntajeEntrevista).HasPrecision(5, 2);
        builder.Property(e => e.PuntajeCompatibilidad).HasPrecision(5, 2);
        builder.Property(e => e.Recomendacion).HasMaxLength(50);
        builder.Property(e => e.Observaciones).HasMaxLength(2000);

        builder.HasOne(e => e.Postulacion)
            .WithMany(e => e.Evaluaciones)
            .HasForeignKey(e => e.PostulacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PostulacionId);
        builder.HasIndex(e => e.EvaluadoAt);
    }
}
