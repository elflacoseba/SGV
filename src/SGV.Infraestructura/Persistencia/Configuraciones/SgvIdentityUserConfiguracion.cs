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

        builder.Property(user => user.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property<string>("ActiveUserNameUnique")
            .HasMaxLength(256)
            .HasColumnType("varchar(256)")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComputedColumnSql(
                "CASE WHEN `IsDeleted` = 0 THEN LOWER(`UserName`) ELSE NULL END",
                stored: true);

        builder.HasIndex("ActiveUserNameUnique")
            .IsUnique()
            .HasDatabaseName("IX_AspNetUsers_ActiveUserNameUnique");

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
