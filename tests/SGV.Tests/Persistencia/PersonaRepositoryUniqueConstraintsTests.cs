using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests verifying unique-constraint enforcement at the MySQL level for active Persona
/// fields (Legajo, Email, Documento), using computed columns defined in PersonaConfiguracion.
/// </summary>
public sealed class PersonaRepositoryUniqueConstraintsTests
{
    // ===================== Duplicate active Legajo =====================

    [MySqlFact]
    public async Task AddAsync_LegajoDuplicadoActivo_LanzaDbUpdateException()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var legajoCompartido = "LEG-UNIQ-" + Guid.NewGuid().ToString("N")[..8];

        var email1 = "leg-dup-a-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona1 = new Persona("Juan", "Pérez", legajoCompartido, email1)
        {
            Id = Guid.NewGuid()
        };
        await repo.AddAsync(persona1, default);
        await context.SaveChangesAsync();

        try
        {
            var email2 = "leg-dup-b-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
            var persona2 = new Persona("Ana", "García", legajoCompartido, email2)
            {
                Id = Guid.NewGuid()
            };
            await repo.AddAsync(persona2, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("unique", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Legajo == legajoCompartido).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== Duplicate active Email =====================

    [MySqlFact]
    public async Task AddAsync_EmailDuplicadoActivo_LanzaDbUpdateException()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailCompartido = "dup-email-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var legajo1 = "LEG-EML1-" + Guid.NewGuid().ToString("N")[..8];
        var legajo2 = "LEG-EML2-" + Guid.NewGuid().ToString("N")[..8];

        var persona1 = new Persona("Juan", "Pérez", legajo1, emailCompartido)
        {
            Id = Guid.NewGuid()
        };
        await repo.AddAsync(persona1, default);
        await context.SaveChangesAsync();

        try
        {
            var persona2 = new Persona("Ana", "García", legajo2, emailCompartido)
            {
                Id = Guid.NewGuid()
            };
            await repo.AddAsync(persona2, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("unique", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Email == emailCompartido).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== Duplicate active Documento =====================

    [MySqlFact]
    public async Task AddAsync_DocumentoDuplicadoActivo_LanzaDbUpdateException()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var legajo1 = "LEG-DOC1-" + Guid.NewGuid().ToString("N")[..8];
        var legajo2 = "LEG-DOC2-" + Guid.NewGuid().ToString("N")[..8];
        var tipoDoc = "DNI";
        var numDoc = "UNIQ-DOC-" + Guid.NewGuid().ToString("N")[..8];
        var email1 = "doc-dup-a-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var email2 = "doc-dup-b-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";

        var persona1 = new Persona("Juan", "Pérez", legajo1, email1)
        {
            Id = Guid.NewGuid()
        };
        persona1.CambiarDocumento(tipoDoc, numDoc);
        await repo.AddAsync(persona1, default);
        await context.SaveChangesAsync();

        try
        {
            var persona2 = new Persona("Ana", "García", legajo2, email2)
            {
                Id = Guid.NewGuid()
            };
            persona2.CambiarDocumento(tipoDoc, numDoc);
            await repo.AddAsync(persona2, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("unique", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.NumeroDocumento == numDoc).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== Reuse after soft-delete =====================

    [MySqlFact]
    public async Task AddAsync_LegajoReutilizadoTrasBaja_PermiteCreacion()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var legajoReutilizado = "LEG-REUSE-" + Guid.NewGuid().ToString("N")[..8];

        var persona = new Persona("Juan", "Pérez", legajoReutilizado, "juan_reuse@test.com")
        {
            Id = Guid.NewGuid()
        };
        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        // Soft-delete the persona
        await repo.DeleteAsync(persona.Id, default);
        await context.SaveChangesAsync();

        try
        {
            // Now create a new persona with the same legajo — should succeed
            var nueva = new Persona("Ana", "García", legajoReutilizado, "ana_reuse@test.com")
            {
                Id = Guid.NewGuid()
            };
            await repo.AddAsync(nueva, default);
            await context.SaveChangesAsync(); // Should not throw

            var obtenida = await repo.GetByIdAsync(nueva.Id, default);
            Assert.NotNull(obtenida);
            Assert.Equal(legajoReutilizado, obtenida!.Legajo);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Legajo == legajoReutilizado).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_EmailReutilizadoTrasBaja_PermiteCreacion()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailReutilizado = "reuse-email-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var legajo1 = "LEG-RSEML1-" + Guid.NewGuid().ToString("N")[..8];

        var persona = new Persona("Juan", "Pérez", legajo1, emailReutilizado)
        {
            Id = Guid.NewGuid()
        };
        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        await repo.DeleteAsync(persona.Id, default);
        await context.SaveChangesAsync();

        try
        {
            var nueva = new Persona("Ana", "García", "LEG-RSEML2-" + Guid.NewGuid().ToString("N")[..8], emailReutilizado)
            {
                Id = Guid.NewGuid()
            };
            await repo.AddAsync(nueva, default);
            await context.SaveChangesAsync();

            var obtenida = await repo.GetByIdAsync(nueva.Id, default);
            Assert.NotNull(obtenida);
            Assert.Equal(emailReutilizado, obtenida!.Email);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Email == emailReutilizado).ToListAsync());
            await context.SaveChangesAsync();
        }
    }
}
