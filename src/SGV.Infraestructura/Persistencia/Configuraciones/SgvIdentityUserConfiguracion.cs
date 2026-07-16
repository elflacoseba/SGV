using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

/// <summary>
/// EF Core configuration for <see cref="SgvIdentityUser"/>.
/// </summary>
/// <remarks>
/// Cambio <c>2026-07-15-quita-soft-delete-usuario</c>: la columna
/// <c>IsDeleted</c> y las columnas generadas
/// <c>ActiveUserNameUnique</c> / <c>ActivePersonaIdUnique</c> se
/// retiran. Sin soft-delete, la unicidad vuelve a vivir plana sobre
/// <c>IX_AspNetUsers_PersonaId</c> (UNIQUE) heredada de la migración
/// <c>VincularIdentityUsuariosAPersonas</c> y conservada por la
/// migración forward-only <c>DropSoftDeleteFromAspNetUsers</c>.
/// La separación activa/bloqueada se modela nativamente con
/// <c>LockoutEnd</c> provisto por <see cref="IdentityUser"/>.
/// </remarks>
public sealed class SgvIdentityUserConfiguracion : IEntityTypeConfiguration<SgvIdentityUser>
{
    public void Configure(EntityTypeBuilder<SgvIdentityUser> builder)
    {
        builder.Property(user => user.PersonaId)
            .IsRequired();

        builder.HasOne<PersonaEntity>()
            .WithOne()
            .HasForeignKey<SgvIdentityUser>(user => user.PersonaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AspNetUsers_Personas_PersonaId");
    }
}