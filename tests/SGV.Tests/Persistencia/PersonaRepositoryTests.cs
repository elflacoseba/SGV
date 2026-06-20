using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Repository tests for Persona read and write operations.
/// </summary>
public sealed class PersonaRepositoryTests
{
    // ===================== Read tests =====================

    [MySqlFact]
    public async Task ListAllAsync_RetornaSoloPersonasActivas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var activa = CreatePersonaEntity("LEG-ACT");
        var inactiva = CreatePersonaEntity("LEG-INACT-LIST", isActive: false);
        context.Set<PersonaEntity>().AddRange(activa, inactiva);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.Contains(entidades, e => e.Id == activa.Id);
            Assert.DoesNotContain(entidades, e => e.Id == inactiva.Id);
            Assert.All(entidades, e => Assert.True(e.IsActive));
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(activa, inactiva);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaPersonasOrdenadasPorApellidoNombre()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var primera = CreatePersonaEntity("LEG-ORD1");
        primera.Apellidos = "AAAAA";
        primera.Nombres = "Primero";
        var segunda = CreatePersonaEntity("LEG-ORD2");
        segunda.Apellidos = "BBBBB";
        segunda.Nombres = "Segundo";

        context.Set<PersonaEntity>().AddRange(primera, segunda);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            var entidadesFiltradas = entidades
                .Where(e => e.Id == primera.Id || e.Id == segunda.Id)
                .ToList();

            Assert.Equal(2, entidadesFiltradas.Count);
            Assert.Equal(primera.Id, entidadesFiltradas[0].Id);
            Assert.Equal(segunda.Id, entidadesFiltradas[1].Id);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(primera, segunda);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);

        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    [MySqlFact]
    public async Task GetByIdAsync_ExcluyePersonasInactivas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("INACT", isActive: false);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ===================== Write tests =====================

    [MySqlFact]
    public async Task AddAsync_AgregaPersona_YLuegoSePuedeConsultar()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailUnico = "addtest-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona = new Persona("Juan", "Pérez", "LEG-TEST-" + Guid.NewGuid().ToString("N")[..8], emailUnico)
        {
            Id = Guid.NewGuid()
        };
        persona.CambiarDocumento("DNI", "12345678-" + Guid.NewGuid().ToString("N")[..8]);

        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        try
        {
            var obtenido = await repo.GetByIdAsync(persona.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(persona.Legajo, obtenido!.Legajo);
            Assert.Equal(persona.Nombres, obtenido.Nombres);
            Assert.Equal(persona.Apellidos, obtenido.Apellidos);
            Assert.Equal(persona.Email, obtenido.Email);
            Assert.Equal(persona.TipoDocumento, obtenido.TipoDocumento);
            Assert.Equal(persona.NumeroDocumento, obtenido.NumeroDocumento);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Id == persona.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_NoIncluyeRelacionesFueraDeAlcance()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailUnico = "norel-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona = new Persona("Juan", "Pérez", "LEG-NO-REL-" + Guid.NewGuid().ToString("N")[..8], emailUnico)
        {
            Id = Guid.NewGuid()
        };

        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        try
        {
            // Verify the PersonaEntity is loaded without Habilidades or Ocupaciones
            var entity = await context.Set<PersonaEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == persona.Id);

            Assert.NotNull(entity);
            Assert.NotNull(entity!.Habilidades);
            Assert.Empty(entity.Habilidades);
            Assert.NotNull(entity.Ocupaciones);
            Assert.Empty(entity.Ocupaciones);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Id == persona.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_RetornaPersonaActiva()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-UPD");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_PersonaInactiva_RetornaNull()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-INACT", isActive: false);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingDeletedAsync_RetornaPersonaInactiva()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-DEL", isActive: false, isDeleted: true);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ModificaCampos()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-MOD");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var persona = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(persona);

            persona!.CambiarDatos("Modificado", "ApellidoMod", "LEG-MOD", "mod@test.com", "555-9999");
            persona.CambiarDocumento("Pasaporte", "AB123456");
            await repo.UpdateAsync(persona, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal("Modificado", modificado!.Nombres);
            Assert.Equal("ApellidoMod", modificado.Apellidos);
            Assert.Equal("LEG-MOD", modificado.Legajo);
            Assert.Equal("mod@test.com", modificado.Email);
            Assert.Equal("555-9999", modificado.Telefono);
            Assert.Equal("Pasaporte", modificado.TipoDocumento);
            Assert.Equal("AB123456", modificado.NumeroDocumento);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaComoInactivo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-DEL2");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var activo = await repo.GetByIdAsync(entity.Id, default);
            Assert.Null(activo);

            var incluyendoEliminado = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);
            Assert.NotNull(incluyendoEliminado);
            Assert.False(incluyendoEliminado!.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_RestauraEstadoActivo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-REACT");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            await repo.ReactivateAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var reactivado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(reactivado);
            Assert.True(reactivado!.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    // ===================== ExistsActive checks =====================

    [MySqlFact]
    public async Task ExistsActiveLegajoAsync_LegajoExistente_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EXIST");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveLegajoAsync(entity.Legajo!, default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveLegajoAsync_ExcluyendoId_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EXCL");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveLegajoAsync(entity.Legajo!, entity.Id, default);

            Assert.False(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveEmailAsync_EmailExistente_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EML", email: "existente@test.com");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveEmailAsync("existente@test.com", default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private static PersonaEntity CreatePersonaEntity(
        string prefix,
        bool isActive = true,
        bool isDeleted = false,
        string? email = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Legajo = $"{prefix}-{suffix}",
            Nombres = $"Nombre {prefix}",
            Apellidos = $"Apellido {prefix}",
            Email = email ?? $"{prefix.ToLowerInvariant()}@test.com",
            IsActive = isActive,
            IsDeleted = isDeleted
        };
    }
}
