using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Habilidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class HabilidadConfiguracion : IEntityTypeConfiguration<Habilidad>
{
    public void Configure(EntityTypeBuilder<Habilidad> builder)
    {
        builder.ToTable("Habilidades");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Categoria).HasMaxLength(100);
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasIndex(e => e.Codigo).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Categoria);
    }
}
