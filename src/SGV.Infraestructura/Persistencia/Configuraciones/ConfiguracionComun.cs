using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

internal static class ConfiguracionComun
{
    public static void ConfigurarAuditoria<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntityBase
    {
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450);
        builder.Property(e => e.UpdatedByUserId).HasMaxLength(450);
        builder.Property(e => e.DeletedByUserId).HasMaxLength(450);
        builder.HasIndex(e => e.IsDeleted);
    }

    public static void ConfigurarId<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : EntityBase
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
