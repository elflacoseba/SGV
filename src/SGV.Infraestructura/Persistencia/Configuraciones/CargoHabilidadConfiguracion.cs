using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Habilidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class CargoHabilidadConfiguracion : IEntityTypeConfiguration<CargoHabilidad>
{
    public void Configure(EntityTypeBuilder<CargoHabilidad> builder)
    {
        builder.ToTable("CargoHabilidades", table =>
            table.HasCheckConstraint("CK_CargoHabilidades_Ponderacion", "`Ponderacion` > 0"));
        builder.ConfigurarId();

        builder.Property(e => e.Ponderacion).HasPrecision(5, 2);

        builder.HasOne(e => e.Cargo)
            .WithMany(e => e.Habilidades)
            .HasForeignKey(e => e.CargoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Habilidad)
            .WithMany()
            .HasForeignKey(e => e.HabilidadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.NivelRequerido)
            .WithMany()
            .HasForeignKey(e => e.NivelRequeridoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CargoId, e.HabilidadId }).IsUnique();
        builder.HasIndex(e => e.HabilidadId);
    }
}
