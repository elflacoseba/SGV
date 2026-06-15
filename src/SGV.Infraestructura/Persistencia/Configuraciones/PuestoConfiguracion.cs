using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PuestoConfiguracion : IEntityTypeConfiguration<PuestoEntity>
{
    public void Configure(EntityTypeBuilder<PuestoEntity> builder)
    {
        builder.ToTable("Puestos", table =>
            table.HasCheckConstraint("CK_Puestos_PuestoSuperior", "`PuestoSuperiorId` IS NULL OR `PuestoSuperiorId` <> `Id`"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasOne(e => e.UnidadOrganizativa)
            .WithMany(e => e.Puestos)
            .HasForeignKey(e => e.UnidadOrganizativaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Cargo)
            .WithMany(e => e.Puestos)
            .HasForeignKey(e => e.CargoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PuestoSuperior)
            .WithMany(e => e.PuestosSubordinados)
            .HasForeignKey(e => e.PuestoSuperiorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<string?>("ActiveCodigoUnique")
            .HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveCodigoUnique").IsUnique();

        builder.HasIndex(e => e.UnidadOrganizativaId);
        builder.HasIndex(e => e.CargoId);
        builder.HasIndex(e => e.PuestoSuperiorId);
    }
}
