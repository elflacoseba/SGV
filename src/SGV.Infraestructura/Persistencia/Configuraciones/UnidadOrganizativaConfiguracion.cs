using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Organizacion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class UnidadOrganizativaConfiguracion : IEntityTypeConfiguration<UnidadOrganizativa>
{
    public void Configure(EntityTypeBuilder<UnidadOrganizativa> builder)
    {
        builder.ToTable("UnidadesOrganizativas", table =>
            table.HasCheckConstraint("CK_UnidadesOrganizativas_UnidadPadre", "[UnidadPadreId] IS NULL OR [UnidadPadreId] <> [Id]"));
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TipoUnidad).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasOne(e => e.UnidadPadre)
            .WithMany(e => e.UnidadesHijas)
            .HasForeignKey(e => e.UnidadPadreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Codigo).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.UnidadPadreId);
        builder.HasIndex(e => e.Nombre);
    }
}
