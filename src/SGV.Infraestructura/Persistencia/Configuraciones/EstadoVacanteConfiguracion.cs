using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Vacantes;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class EstadoVacanteConfiguracion : IEntityTypeConfiguration<EstadoVacante>
{
    public void Configure(EntityTypeBuilder<EstadoVacante> builder)
    {
        builder.ToTable("EstadosVacante");
        builder.ConfigurarId();
        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Codigo).IsUnique();
    }
}
