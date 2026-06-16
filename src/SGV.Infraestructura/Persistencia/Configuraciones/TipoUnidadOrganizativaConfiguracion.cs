using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class TipoUnidadOrganizativaConfiguracion : IEntityTypeConfiguration<TipoUnidadOrganizativaEntity>
{
    public void Configure(EntityTypeBuilder<TipoUnidadOrganizativaEntity> builder)
    {
        builder.ToTable("TiposUnidadOrganizativa", table =>
            table.HasCheckConstraint("CK_TiposUnidadOrganizativa_Codigo", "`Codigo` <> ''"));
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

        builder.HasIndex(e => e.Codigo)
            .IsUnique()
            .HasDatabaseName("IX_TiposUnidadOrganizativa_Codigo");
    }
}
