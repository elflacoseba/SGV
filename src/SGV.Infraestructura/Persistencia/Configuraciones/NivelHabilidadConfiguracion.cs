using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class NivelHabilidadConfiguracion : IEntityTypeConfiguration<NivelHabilidadEntity>
{
    public void Configure(EntityTypeBuilder<NivelHabilidadEntity> builder)
    {
        builder.ToTable("NivelesHabilidad", table =>
            table.HasCheckConstraint("CK_NivelesHabilidad_ValorNumerico", "`ValorNumerico` BETWEEN 1 AND 4"));
        builder.ConfigurarId();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasIndex(e => e.ValorNumerico).IsUnique();
    }
}
