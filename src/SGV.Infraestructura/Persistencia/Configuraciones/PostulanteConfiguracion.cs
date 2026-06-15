using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PostulanteConfiguracion : IEntityTypeConfiguration<PostulanteEntity>
{
    public void Configure(EntityTypeBuilder<PostulanteEntity> builder)
    {
        builder.ToTable("Postulantes");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Nombres).HasMaxLength(100);
        builder.Property(e => e.Apellidos).HasMaxLength(100);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Telefono).HasMaxLength(50);
        builder.Property(e => e.Fuente).HasMaxLength(100);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.HasOne(e => e.Persona)
            .WithMany()
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        // MySQL does not support filtered indexes. Use a generated column
        // that is NULL when PersonaId is NULL or the record is soft-deleted.
        builder.Property<Guid?>("ActivePersonaIdUnique")
            .HasComputedColumnSql("CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END")
            .IsRequired(false);
        builder.HasIndex("ActivePersonaIdUnique").IsUnique();

        builder.HasIndex(e => new { e.Apellidos, e.Nombres });
        builder.HasIndex(e => e.Email);
    }
}
