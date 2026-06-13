using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Seleccion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class EstadoPostulacionConfiguracion : IEntityTypeConfiguration<EstadoPostulacion>
{
    public void Configure(EntityTypeBuilder<EstadoPostulacion> builder)
    {
        builder.ToTable("EstadosPostulacion");
        builder.ConfigurarId();
        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Codigo).IsUnique();
    }
}
