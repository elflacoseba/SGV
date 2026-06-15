using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PersonaHabilidadConfiguracion : IEntityTypeConfiguration<PersonaHabilidadEntity>
{
    public void Configure(EntityTypeBuilder<PersonaHabilidadEntity> builder)
    {
        builder.ToTable("PersonaHabilidades");
        builder.ConfigurarId();

        builder.Property(e => e.Fuente).HasMaxLength(100);

        builder.HasOne(e => e.Persona)
            .WithMany(e => e.Habilidades)
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Habilidad)
            .WithMany()
            .HasForeignKey(e => e.HabilidadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.NivelHabilidad)
            .WithMany()
            .HasForeignKey(e => e.NivelHabilidadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonaId, e.HabilidadId }).IsUnique();
        builder.HasIndex(e => e.HabilidadId);
    }
}
