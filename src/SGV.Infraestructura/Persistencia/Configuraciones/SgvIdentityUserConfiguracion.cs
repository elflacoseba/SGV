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

        // PR #148 review: la unicidad plana sobre PersonaId bloquea la
        // recreación de un usuario cuando el previo fue dado de baja
        // lógica. MySQL no soporta índices filtrados, así que seguimos el
        // mismo patrón que ActiveUserNameUnique: una columna generada
        // STORED que devuelve NULL cuando IsDeleted = 1, de modo que las
        // filas soft-deleted NO participan de la unicidad.
        //
        // Seguimos la convención de PostulanteConfiguracion para
        // columnas generadas sobre Guid: propiedad shadow Guid? (no
        // string?) porque el proveedor de Pomelo sabe convertir
        // nativamente Guid ↔ char(36) — declarar la propiedad como
        // string? dispara InvalidCastException al releer la fila
        // resultante del INSERT (Guid no se castea a String).
        builder.Property<Guid?>("ActivePersonaIdUnique")
            .HasComputedColumnSql(
                "CASE WHEN `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END")
            .IsRequired(false);

        builder.HasIndex("ActivePersonaIdUnique")
            .IsUnique()
            .HasDatabaseName("IX_AspNetUsers_ActivePersonaIdUnique");

        builder.HasOne<PersonaEntity>()
            .WithOne()
            .HasForeignKey<SgvIdentityUser>(user => user.PersonaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AspNetUsers_Personas_PersonaId");

        // PR #148 review: anulamos el índice único plano que EF Core
        // auto-genera para la FK 1:1 (`IX_AspNetUsers_PersonaId`). La
        // unicidad soft-delete-aware vive ahora exclusivamente en
        // `ActivePersonaIdUnique` (NULL cuando IsDeleted=1). Sin este
        // override, el FK 1:1 seguiría exigiendo un `PersonaId`
        // globalmente único y bloqueando la reactivación de un usuario
        // soft-deleted. EF permite neutralizar el índice del FK
        // configurando explícitamente el mismo índice como no-único.
        builder.HasIndex(user => user.PersonaId)
            .IsUnique(false)
            .HasDatabaseName("IX_AspNetUsers_PersonaId");
    }
}
