using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Organizacion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class CargoConfiguracion : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("Cargos");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Nivel).HasMaxLength(50);
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasIndex(e => e.Codigo).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Nombre);
    }
}
