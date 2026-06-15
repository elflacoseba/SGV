using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class EstadoPostulacionConfiguracion : IEntityTypeConfiguration<EstadoPostulacionEntity>
{
    public void Configure(EntityTypeBuilder<EstadoPostulacionEntity> builder)
    {
        builder.ToTable("EstadosPostulacion");
        builder.ConfigurarId();
        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Codigo).IsUnique();
    }
}
