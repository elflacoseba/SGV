using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Seleccion;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PostulanteConfiguracion : IEntityTypeConfiguration<Postulante>
{
    public void Configure(EntityTypeBuilder<Postulante> builder)
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

        builder.HasIndex(e => e.PersonaId).IsUnique().HasFilter("[PersonaId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Apellidos, e.Nombres });
        builder.HasIndex(e => e.Email);
    }
}
