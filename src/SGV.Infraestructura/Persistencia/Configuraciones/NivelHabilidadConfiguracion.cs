using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Habilidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class NivelHabilidadConfiguracion : IEntityTypeConfiguration<NivelHabilidad>
{
    public void Configure(EntityTypeBuilder<NivelHabilidad> builder)
    {
        builder.ToTable("NivelesHabilidad", table =>
            table.HasCheckConstraint("CK_NivelesHabilidad_ValorNumerico", "[ValorNumerico] BETWEEN 1 AND 4"));
        builder.ConfigurarId();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasIndex(e => e.ValorNumerico).IsUnique();
    }
}
