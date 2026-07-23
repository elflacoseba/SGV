using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

/// <summary>
/// Mapeo EF Core para el catálogo <c>CategoriaHabilidad</c>
/// (issue migrar-campo-categoria-habilidades-a-tabla). Esquema alineado
/// con <see cref="TipoDocumentoConfiguracion"/> (issue #147):
///   - Tabla: <c>CategoriasHabilidad</c> (sin <c>IsActive</c>/<c>IsDeleted</c>).
///   - PK: <c>Id</c> (<c>char(36)</c>).
///   - <c>Codigo varchar(50) NOT NULL UNIQUE</c> con <c>ascii_general_ci</c>.
///   - <c>Nombre varchar(100) NOT NULL</c>.
/// </summary>
public sealed class CategoriaHabilidadConfiguracion : IEntityTypeConfiguration<CategoriaHabilidadEntity>
{
    public void Configure(EntityTypeBuilder<CategoriaHabilidadEntity> builder)
    {
        builder.ToTable("CategoriasHabilidad", table =>
        {
            table.HasCheckConstraint("CK_CategoriasHabilidad_Codigo", "`Codigo` <> ''");
        });
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
            .HasDatabaseName("IX_CategoriasHabilidad_Codigo");
    }
}