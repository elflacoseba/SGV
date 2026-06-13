using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Dominio.Personas;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class PersonaConfiguracion : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("Personas");
        builder.ConfigurarId();
        builder.ConfigurarAuditoria();

        builder.Property(e => e.Legajo).HasMaxLength(50);
        builder.Property(e => e.Nombres).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Apellidos).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.TipoDocumento).HasMaxLength(50);
        builder.Property(e => e.NumeroDocumento).HasMaxLength(50);
        builder.Property(e => e.Telefono).HasMaxLength(50);

        builder.HasIndex(e => e.Legajo).IsUnique().HasFilter("[Legajo] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Email).IsUnique().HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.TipoDocumento, e.NumeroDocumento }).IsUnique().HasFilter("[TipoDocumento] IS NOT NULL AND [NumeroDocumento] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Apellidos, e.Nombres });
    }
}
