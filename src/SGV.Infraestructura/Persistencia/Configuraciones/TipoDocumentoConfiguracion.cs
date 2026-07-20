using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

/// <summary>
/// Mapeo EF Core para el catálogo <c>TipoDocumento</c>.
/// Esquema alineado con el modelo de datos del design (issue #147):
///   - Tabla: <c>TiposDocumento</c> (sin <c>IsActive</c>/<c>IsDeleted</c>).
///   - PK: <c>Id</c> (<c>char(36)</c>).
///   - <c>Codigo varchar(50) NOT NULL UNIQUE</c> con <c>ascii_general_ci</c>.
///   - <c>Nombre varchar(100) NOT NULL</c>.
///   - <c>PatronValidacion varchar(255) NULL</c>.
///   - <c>LongitudMinima</c> / <c>LongitudMaxima</c> como <c>int NULL</c>.
/// </summary>
public sealed class TipoDocumentoConfiguracion : IEntityTypeConfiguration<TipoDocumentoEntity>
{
    public void Configure(EntityTypeBuilder<TipoDocumentoEntity> builder)
    {
        builder.ToTable("TiposDocumento", table =>
        {
            table.HasCheckConstraint("CK_TiposDocumento_Codigo", "`Codigo` <> ''");
            table.HasCheckConstraint(
                "CK_TiposDocumento_Longitudes",
                "`LongitudMinima` IS NULL OR `LongitudMaxima` IS NULL OR `LongitudMinima` <= `LongitudMaxima`");
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
        builder.Property(e => e.PatronValidacion)
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);
        builder.Property(e => e.LongitudMinima)
            .HasColumnType("int");
        builder.Property(e => e.LongitudMaxima)
            .HasColumnType("int");

        builder.HasIndex(e => e.Codigo)
            .IsUnique()
            .HasDatabaseName("IX_TiposDocumento_Codigo");
    }
}
