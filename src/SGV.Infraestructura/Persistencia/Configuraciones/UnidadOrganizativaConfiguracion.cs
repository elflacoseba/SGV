using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class UnidadOrganizativaConfiguracion : IEntityTypeConfiguration<UnidadOrganizativaEntity>
{
    public void Configure(EntityTypeBuilder<UnidadOrganizativaEntity> builder)
    {
        builder.ToTable("UnidadesOrganizativas", table =>
            table.HasCheckConstraint("CK_UnidadesOrganizativas_UnidadPadre", "`UnidadPadreId` IS NULL OR `UnidadPadreId` <> `Id`"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasOne(e => e.UnidadPadre)
            .WithMany(e => e.UnidadesHijas)
            .HasForeignKey(e => e.UnidadPadreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TipoUnidadOrganizativa)
            .WithMany()
            .HasForeignKey(e => e.TipoUnidadOrganizativaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TipoUnidadOrganizativaId)
            .HasDatabaseName("IX_UnidadesOrganizativas_TipoUnidadOrganizativaId");

        builder.Property<string?>("ActiveCodigoUnique")
            .HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActiveCodigoUnique").IsUnique();

        builder.HasIndex(e => e.UnidadPadreId);
        builder.HasIndex(e => e.Nombre);

        // Composite indexes for common query patterns (IsActive + IsDeleted first)
        builder.HasIndex(e => new { e.IsActive, e.IsDeleted, e.UnidadPadreId })
            .HasDatabaseName("IX_UnidadesOrganizativas_ActivoPadre");
        builder.HasIndex(e => new { e.IsActive, e.IsDeleted, e.TipoUnidadOrganizativaId })
            .HasDatabaseName("IX_UnidadesOrganizativas_ActivoTipo");
        builder.HasIndex(e => new { e.IsActive, e.IsDeleted, e.Codigo })
            .HasDatabaseName("IX_UnidadesOrganizativas_ActivoCodigo");
    }
}
