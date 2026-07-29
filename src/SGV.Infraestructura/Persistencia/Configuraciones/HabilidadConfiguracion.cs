using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class HabilidadConfiguracion : IEntityTypeConfiguration<HabilidadEntity>
{
    public void Configure(EntityTypeBuilder<HabilidadEntity> builder)
    {
        builder.ToTable("Habilidades");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.Property<string?>("ActiveCodigoUnique")
            .HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END", stored: true)
            .IsRequired(false);
        builder.HasIndex("ActiveCodigoUnique").IsUnique();

        // FK opcional al catálogo CategoriasHabilidad. La FK constraint
        // (FK_Habilidades_CategoriasHabilidad_CategoriaId con OnDelete Restrict)
        // se crea en la migración AddCategoriaHabilidadCatalog.
        builder.HasOne(e => e.Categoria)
            .WithMany()
            .HasForeignKey(e => e.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CategoriaId)
            .HasDatabaseName("IX_Habilidades_CategoriaId");
    }
}