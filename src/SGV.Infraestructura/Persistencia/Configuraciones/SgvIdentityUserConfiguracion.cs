using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

public sealed class SgvIdentityUserConfiguracion : IEntityTypeConfiguration<SgvIdentityUser>
{
    public void Configure(EntityTypeBuilder<SgvIdentityUser> builder)
    {
        builder.Property(user => user.PersonaId)
            .IsRequired();

        builder.HasIndex(user => user.PersonaId)
            .IsUnique()
            .HasDatabaseName("IX_AspNetUsers_PersonaId");

        builder.HasOne<PersonaEntity>()
            .WithOne()
            .HasForeignKey<SgvIdentityUser>(user => user.PersonaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AspNetUsers_Personas_PersonaId");
    }
}
