using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class CargoConfiguracion : IEntityTypeConfiguration<CargoEntity>
{
    public void Configure(EntityTypeBuilder<CargoEntity> builder)
    {
        builder.ToTable("Cargos");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Nivel).HasMaxLength(50);
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        // MySQL does not support filtered indexes. Use a generated column
        // that is NULL for soft-deleted rows so the unique index allows
        // multiple deleted records while enforcing uniqueness among active ones.
        builder.Property<string?>("ActiveCodigoUnique")
            .HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveCodigoUnique").IsUnique();

        builder.HasIndex(e => e.Nombre);
    }
}
