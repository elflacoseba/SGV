using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Persistence tests for PersonaSkillRepository (PersonaHabilidades).
/// Verifies upsert, unique constraint, and physical deletion.
/// </summary>
public sealed class PersonaSkillRepositoryTests
{
    [MySqlFact]
    public async Task AddAsync_AgregaPersonaHabilidad_YLuegoSePuedeConsultar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona = RepositoryTestData.CreatePersona("PSK-ADD");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-HAB");
        await context.Set<PersonaEntity>().AddAsync(persona);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var personaHabilidad = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelBasicoId);

        try
        {
            await repo.AddAsync(personaHabilidad, default);
            await context.SaveChangesAsync();

            var obtenido = await repo.GetByPersonaAndSkillAsync(persona.Id, habilidad.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(persona.Id, obtenido!.PersonaId);
            Assert.Equal(habilidad.Id, obtenido.HabilidadId);
            Assert.Equal(DatosSemilla.NivelBasicoId, obtenido.NivelHabilidadId);
        }
        finally
        {
            context.Set<PersonaHabilidadEntity>().RemoveRange(
                await context.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona.Id && ph.HabilidadId == habilidad.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<PersonaEntity>().Remove(persona);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_DuplicadoPorPersonaHabilidad_LanzaDbUpdateException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona = RepositoryTestData.CreatePersona("PSK-DUP");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-HAB2");
        await context.Set<PersonaEntity>().AddAsync(persona);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var primero = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelBasicoId);
        await repo.AddAsync(primero, default);
        await context.SaveChangesAsync();

        try
        {
            var segundo = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelIntermedioId);
            await repo.AddAsync(segundo, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("Duplicate", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var cleanContext = new TestSgvDbContextFactory().CreateDbContext([]);
            cleanContext.Set<PersonaHabilidadEntity>().RemoveRange(
                await cleanContext.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona.Id)
                    .ToListAsync());
            cleanContext.Set<HabilidadEntity>().RemoveRange(
                await cleanContext.Set<HabilidadEntity>()
                    .Where(h => h.Id == habilidad.Id)
                    .ToListAsync());
            cleanContext.Set<PersonaEntity>().RemoveRange(
                await cleanContext.Set<PersonaEntity>()
                    .Where(p => p.Id == persona.Id)
                    .ToListAsync());
            await cleanContext.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ActualizaNivelHabilidad()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona = RepositoryTestData.CreatePersona("PSK-UPD");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-HAB3");
        await context.Set<PersonaEntity>().AddAsync(persona);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var original = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelBasicoId);
        await repo.AddAsync(original, default);
        await context.SaveChangesAsync();

        try
        {
            var obtenidoAntes = await repo.GetByPersonaAndSkillAsync(persona.Id, habilidad.Id, default);
            Assert.NotNull(obtenidoAntes);
            Assert.Equal(DatosSemilla.NivelBasicoId, obtenidoAntes!.NivelHabilidadId);

            // PersonaHabilidad is immutable after creation; replace via remove+add
            await repo.DeleteAsync(obtenidoAntes, default);
            var actualizado = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelAvanzadoId);
            await repo.AddAsync(actualizado, default);
            await context.SaveChangesAsync();

            var obtenidoDespues = await repo.GetByPersonaAndSkillAsync(persona.Id, habilidad.Id, default);
            Assert.NotNull(obtenidoDespues);
            Assert.Equal(DatosSemilla.NivelAvanzadoId, obtenidoDespues!.NivelHabilidadId);
        }
        finally
        {
            context.Set<PersonaHabilidadEntity>().RemoveRange(
                await context.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona.Id && ph.HabilidadId == habilidad.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<PersonaEntity>().Remove(persona);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_EliminaFisicamente()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona = RepositoryTestData.CreatePersona("PSK-DEL");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-HAB4");
        await context.Set<PersonaEntity>().AddAsync(persona);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var personaHabilidad = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelBasicoId);
        await repo.AddAsync(personaHabilidad, default);
        await context.SaveChangesAsync();

        try
        {
            var existente = await repo.GetByPersonaAndSkillAsync(persona.Id, habilidad.Id, default);
            Assert.NotNull(existente);

            await repo.DeleteAsync(existente!, default);
            await context.SaveChangesAsync();

            var despuesBorrado = await repo.GetByPersonaAndSkillAsync(persona.Id, habilidad.Id, default);
            Assert.Null(despuesBorrado);

            var filasRestantes = await context.Set<PersonaHabilidadEntity>()
                .Where(ph => ph.PersonaId == persona.Id && ph.HabilidadId == habilidad.Id)
                .CountAsync();
            Assert.Equal(0, filasRestantes);
        }
        finally
        {
            context.Set<PersonaHabilidadEntity>().RemoveRange(
                await context.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<PersonaEntity>().Remove(persona);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListDetailedByPersonaIdAsync_RetornaNestedSkillYNivel()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona = RepositoryTestData.CreatePersona("PSK-DET");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-DET-HAB");
        await context.Set<PersonaEntity>().AddAsync(persona);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var asignacion = new PersonaHabilidad(persona.Id, habilidad.Id, DatosSemilla.NivelBasicoId);
        await repo.AddAsync(asignacion, default);
        await context.SaveChangesAsync();

        try
        {
            var resultado = await repo.ListDetailedByPersonaIdAsync(persona.Id, default);

            Assert.Single(resultado);
            Assert.Equal(habilidad.Id, resultado[0].Skill.Id);
            Assert.Equal(DatosSemilla.NivelBasicoId, resultado[0].Nivel.Id);
            Assert.NotNull(resultado[0].Skill);
            Assert.Equal(habilidad.Codigo, resultado[0].Skill.Codigo);
            Assert.Equal(habilidad.Nombre, resultado[0].Skill.Nombre);
            Assert.NotNull(resultado[0].Nivel);
            Assert.Equal("Básico", resultado[0].Nivel.Nombre);
            Assert.Equal((byte)1, resultado[0].Nivel.ValorNumerico);
        }
        finally
        {
            context.Set<PersonaHabilidadEntity>().RemoveRange(
                await context.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<PersonaEntity>().Remove(persona);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListDetailedByPersonaIdAsync_SinAsignaciones_RetornaVacio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var resultado = await repo.ListDetailedByPersonaIdAsync(Guid.NewGuid(), default);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [MySqlFact]
    public async Task ListByPersonaIdAsync_RetornaSoloLasDeLaPersona()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var persona1 = RepositoryTestData.CreatePersona("PSK-L1");
        var persona2 = RepositoryTestData.CreatePersona("PSK-L2");
        var habilidad = RepositoryTestData.CreateHabilidad("PSK-HAB5");
        var habilidad2 = RepositoryTestData.CreateHabilidad("PSK-HAB6");
        await context.Set<PersonaEntity>().AddRangeAsync(persona1, persona2);
        await context.Set<HabilidadEntity>().AddRangeAsync(habilidad, habilidad2);
        await context.SaveChangesAsync();

        var asignacion1 = new PersonaHabilidad(persona1.Id, habilidad.Id, DatosSemilla.NivelBasicoId);
        var asignacion2 = new PersonaHabilidad(persona2.Id, habilidad2.Id, DatosSemilla.NivelIntermedioId);
        await repo.AddAsync(asignacion1, default);
        await repo.AddAsync(asignacion2, default);
        await context.SaveChangesAsync();

        try
        {
            var resultado = await repo.ListByPersonaIdAsync(persona1.Id, default);

            Assert.Single(resultado);
            Assert.Equal(persona1.Id, resultado[0].PersonaId);
            Assert.Equal(habilidad.Id, resultado[0].HabilidadId);
        }
        finally
        {
            context.Set<PersonaHabilidadEntity>().RemoveRange(
                await context.Set<PersonaHabilidadEntity>()
                    .Where(ph => ph.PersonaId == persona1.Id || ph.PersonaId == persona2.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().RemoveRange(habilidad, habilidad2);
            context.Set<PersonaEntity>().RemoveRange(persona1, persona2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByPersonaAndSkillAsync_NoExistente_RetornaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var resultado = await repo.GetByPersonaAndSkillAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [MySqlFact]
    public async Task ListByPersonaIdAsync_SinAsignaciones_RetornaVacio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaSkillRepository(context);

        var resultado = await repo.ListByPersonaIdAsync(Guid.NewGuid(), default);

        Assert.Empty(resultado);
    }
}
