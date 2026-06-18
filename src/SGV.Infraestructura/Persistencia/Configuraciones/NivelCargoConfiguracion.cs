using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class NivelCargoConfiguracion : IEntityTypeConfiguration<NivelCargoEntity>
{
    public void Configure(EntityTypeBuilder<NivelCargoEntity> builder)
    {
        builder.ToTable("NivelesCargo", table =>
            table.HasCheckConstraint("CK_NivelesCargo_ValorNumerico", "`ValorNumerico` >= 0 AND `ValorNumerico` <= 255"));
        builder.ConfigurarId();

        builder.Property(e => e.Codigo)
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired()
            .UseCollation("ascii_general_ci");
        builder.Property(e => e.Nombre)
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(e => e.ValorNumerico)
            .HasColumnType("tinyint unsigned")
            .IsRequired();
        builder.Property(e => e.Orden)
            .HasColumnType("int")
            .IsRequired();

        builder.HasIndex(e => e.Codigo)
            .IsUnique()
            .HasDatabaseName("IX_NivelesCargo_Codigo");
    }
}
