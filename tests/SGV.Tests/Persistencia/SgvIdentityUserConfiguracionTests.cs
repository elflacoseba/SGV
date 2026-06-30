using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class SgvIdentityUserConfiguracionTests
{
    private readonly SgvDbContext _context = new TestSgvDbContextFactory().CreateDbContext([]);

    [Fact]
    public void SgvIdentityUser_ConfiguresRequiredPersonaId()
    {
        var entity = _context.Model.FindEntityType(typeof(SgvIdentityUser));

        var personaId = entity!.FindProperty(nameof(SgvIdentityUser.PersonaId));
        Assert.NotNull(personaId);
        Assert.False(personaId!.IsNullable);
        Assert.Equal(typeof(Guid), personaId.ClrType);
    }

    [Fact]
    public void SgvIdentityUser_ConfiguresPersonaForeignKeyWithRestrictDelete()
    {
        var entity = _context.Model.FindEntityType(typeof(SgvIdentityUser));

        var foreignKey = entity!.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(PersonaEntity));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal([nameof(SgvIdentityUser.PersonaId)], foreignKey.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void SgvIdentityUser_ConfiguresUniquePersonaIdIndex()
    {
        var entity = _context.Model.FindEntityType(typeof(SgvIdentityUser));

        var index = entity!.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(SgvIdentityUser.PersonaId)]));
        Assert.True(index.IsUnique);
    }
}

public sealed class IdentityUserPersistenceTests
{
    private static string UniqueUserId() => Guid.NewGuid().ToString("N");
    private static string UniqueUserName() => "testuser-" + Guid.NewGuid().ToString("N")[..8];
    private static string UniqueEmail() => $"test-{Guid.NewGuid().ToString("N")[..8]}@test.com";

    [MySqlFact]
    public async Task IdentityUser_LinkedToPersona_SurvivesPersonaDeactivateAndReactivate()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        // Arrange: create a Persona and an SgvIdentityUser linked to it
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Legajo = "LINK-" + Guid.NewGuid().ToString("N")[..8],
            Nombres = "Test",
            Apellidos = "User",
            Email = UniqueEmail(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Set<PersonaEntity>().Add(persona);

        var userId = UniqueUserId();
        var userName = UniqueUserName();
        var identityUser = new SgvIdentityUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = UniqueEmail(),
            NormalizedEmail = UniqueEmail().ToUpperInvariant(),
            PersonaId = persona.Id,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        context.Set<SgvIdentityUser>().Add(identityUser);
        await context.SaveChangesAsync();

        try
        {
            // Act: deactivate the persona (simulating PersonaServicioComandos.DesactivarAsync)
            persona.IsActive = false;
            persona.IsDeleted = true;
            persona.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // Assert: the Identity user still exists with the same PersonaId
            var userAfterDeactivate = await context.Set<SgvIdentityUser>().FindAsync(userId);
            Assert.NotNull(userAfterDeactivate);
            Assert.Equal(persona.Id, userAfterDeactivate!.PersonaId);

            // Act: reactivate the persona (simulating PersonaServicioComandos.ReactivarAsync)
            persona.IsActive = true;
            persona.IsDeleted = false;
            persona.DeletedAt = null;
            await context.SaveChangesAsync();

            // Assert: the link is still intact after reactivation
            var userAfterReactivate = await context.Set<SgvIdentityUser>().FindAsync(userId);
            Assert.NotNull(userAfterReactivate);
            Assert.Equal(persona.Id, userAfterReactivate!.PersonaId);
        }
        finally
        {
            // Cleanup
            var toRemove = await context.Set<SgvIdentityUser>().FindAsync(userId);
            if (toRemove is not null)
                context.Set<SgvIdentityUser>().Remove(toRemove);
            context.Set<PersonaEntity>().Remove(persona);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task SaveIdentityUser_WithInvalidPersonaId_ThrowsDbUpdateException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var invalidPersonaId = Guid.NewGuid(); // Does not exist in Personas table

        var invalidUser = new SgvIdentityUser
        {
            Id = UniqueUserId(),
            UserName = UniqueUserName(),
            NormalizedUserName = UniqueUserName().ToUpperInvariant(),
            Email = UniqueEmail(),
            NormalizedEmail = UniqueEmail().ToUpperInvariant(),
            PersonaId = invalidPersonaId,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };

        context.Set<SgvIdentityUser>().Add(invalidUser);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Contains("FK_AspNetUsers_Personas_PersonaId", ex.InnerException?.Message, StringComparison.OrdinalIgnoreCase);
    }
}
