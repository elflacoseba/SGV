using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Persistence tests for CargoSkillRepository (CargoHabilidades).
/// Verifies upsert, unique constraint, and physical deletion.
/// </summary>
public sealed class CargoSkillRepositoryTests
{
    [MySqlFact]
    public async Task AddAsync_AgregaCargoHabilidad_YLuegoSePuedeConsultar()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo = RepositoryTestData.CreateCargo("CSK-ADD", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-HAB");
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var cargoHabilidad = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);

        try
        {
            await repo.AddAsync(cargoHabilidad, default);
            await context.SaveChangesAsync();

            var obtenido = await repo.GetByCargoAndSkillAsync(cargo.Id, habilidad.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(cargo.Id, obtenido!.CargoId);
            Assert.Equal(habilidad.Id, obtenido.HabilidadId);
            Assert.Equal(DatosSemilla.NivelBasicoId, obtenido.NivelRequeridoId);
            Assert.Equal(1.0m, obtenido.Ponderacion);
            Assert.True(obtenido.EsObligatoria);
        }
        finally
        {
            context.Set<CargoHabilidadEntity>().RemoveRange(
                await context.Set<CargoHabilidadEntity>()
                    .Where(ch => ch.CargoId == cargo.Id && ch.HabilidadId == habilidad.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<CargoEntity>().Remove(cargo);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_DuplicadoPorCargoHabilidad_LanzaDbUpdateException()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo = RepositoryTestData.CreateCargo("CSK-DUP", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-HAB2");
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var primero = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);
        await repo.AddAsync(primero, default);
        await context.SaveChangesAsync();

        try
        {
            var segundo = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelIntermedioId, 2.0m, false);
            await repo.AddAsync(segundo, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("Duplicate", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Use a fresh context for cleanup to avoid tracking conflicts
            await using var cleanContext = new SgvDbContextFactory().CreateDbContext([]);
            cleanContext.Set<CargoHabilidadEntity>().RemoveRange(
                await cleanContext.Set<CargoHabilidadEntity>()
                    .Where(ch => ch.CargoId == cargo.Id)
                    .ToListAsync());
            cleanContext.Set<HabilidadEntity>().RemoveRange(
                await cleanContext.Set<HabilidadEntity>()
                    .Where(h => h.Id == habilidad.Id)
                    .ToListAsync());
            cleanContext.Set<CargoEntity>().RemoveRange(
                await cleanContext.Set<CargoEntity>()
                    .Where(c => c.Id == cargo.Id)
                    .ToListAsync());
            await cleanContext.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ActualizaNivelRequerido()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo = RepositoryTestData.CreateCargo("CSK-UPD", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-HAB3");
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var original = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);
        await repo.AddAsync(original, default);
        await context.SaveChangesAsync();

        try
        {
            // Fetch, then update nivel
            var obtenidoAntes = await repo.GetByCargoAndSkillAsync(cargo.Id, habilidad.Id, default);
            Assert.NotNull(obtenidoAntes);
            Assert.Equal(DatosSemilla.NivelBasicoId, obtenidoAntes!.NivelRequeridoId);

            // CargoHabilidad is immutable after creation; update approach:
            // Remove old and add new (same pattern as CargoSkillServicio)
            await repo.DeleteAsync(obtenidoAntes, default);
            var actualizado = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelAvanzadoId, 2.0m, false);
            await repo.AddAsync(actualizado, default);
            await context.SaveChangesAsync();

            var obtenidoDespues = await repo.GetByCargoAndSkillAsync(cargo.Id, habilidad.Id, default);
            Assert.NotNull(obtenidoDespues);
            Assert.Equal(DatosSemilla.NivelAvanzadoId, obtenidoDespues!.NivelRequeridoId);
            Assert.Equal(2.0m, obtenidoDespues.Ponderacion);
        }
        finally
        {
            context.Set<CargoHabilidadEntity>().RemoveRange(
                await context.Set<CargoHabilidadEntity>()
                    .Where(ch => ch.CargoId == cargo.Id && ch.HabilidadId == habilidad.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<CargoEntity>().Remove(cargo);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_EliminaFisicamente()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo = RepositoryTestData.CreateCargo("CSK-DEL", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-HAB4");
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var cargoHabilidad = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);
        await repo.AddAsync(cargoHabilidad, default);
        await context.SaveChangesAsync();

        try
        {
            // Verify it exists
            var existente = await repo.GetByCargoAndSkillAsync(cargo.Id, habilidad.Id, default);
            Assert.NotNull(existente);

            await repo.DeleteAsync(existente!, default);
            await context.SaveChangesAsync();

            // Verify physical deletion
            var despuesBorrado = await repo.GetByCargoAndSkillAsync(cargo.Id, habilidad.Id, default);
            Assert.Null(despuesBorrado);

            // Verify no row at all (not soft-deleted)
            var filasRestantes = await context.Set<CargoHabilidadEntity>()
                .Where(ch => ch.CargoId == cargo.Id && ch.HabilidadId == habilidad.Id)
                .CountAsync();
            Assert.Equal(0, filasRestantes);
        }
        finally
        {
            context.Set<CargoHabilidadEntity>().RemoveRange(
                await context.Set<CargoHabilidadEntity>()
                    .Where(ch => ch.CargoId == cargo.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<CargoEntity>().Remove(cargo);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListDetailedByCargoIdAsync_RetornaNestedSkillYNivel()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo = RepositoryTestData.CreateCargo("CSK-DET", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-DET-HAB");
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<HabilidadEntity>().AddAsync(habilidad);
        await context.SaveChangesAsync();

        var asignacion = new CargoHabilidad(cargo.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);
        await repo.AddAsync(asignacion, default);
        await context.SaveChangesAsync();

        try
        {
            var resultado = await repo.ListDetailedByCargoIdAsync(cargo.Id, default);

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
            context.Set<CargoHabilidadEntity>().RemoveRange(
                await context.Set<CargoHabilidadEntity>()
                    .Where(ch => ch.CargoId == cargo.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().Remove(habilidad);
            context.Set<CargoEntity>().Remove(cargo);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListDetailedByCargoIdAsync_SinAsignaciones_RetornaVacio()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var resultado = await repo.ListDetailedByCargoIdAsync(Guid.NewGuid(), default);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [MySqlFact]
    public async Task ListByCargoIdAsync_RetornaSoloLasDelCargo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var cargo1 = RepositoryTestData.CreateCargo("CSK-L1", NivelCargoConstantes.DirectivoId);
        var cargo2 = RepositoryTestData.CreateCargo("CSK-L2", NivelCargoConstantes.DirectivoId);
        var habilidad = RepositoryTestData.CreateHabilidad("CSK-HAB5");
        var habilidad2 = RepositoryTestData.CreateHabilidad("CSK-HAB6");
        await context.Set<CargoEntity>().AddRangeAsync(cargo1, cargo2);
        await context.Set<HabilidadEntity>().AddRangeAsync(habilidad, habilidad2);
        await context.SaveChangesAsync();

        var asignacion1 = new CargoHabilidad(cargo1.Id, habilidad.Id, DatosSemilla.NivelBasicoId, 1.0m, true);
        var asignacion2 = new CargoHabilidad(cargo2.Id, habilidad2.Id, DatosSemilla.NivelIntermedioId, 2.0m, false);
        await repo.AddAsync(asignacion1, default);
        await repo.AddAsync(asignacion2, default);
        await context.SaveChangesAsync();

        try
        {
            var resultado = await repo.ListByCargoIdAsync(cargo1.Id, default);

            Assert.Single(resultado);
            Assert.Equal(cargo1.Id, resultado[0].CargoId);
            Assert.Equal(habilidad.Id, resultado[0].HabilidadId);
        }
        finally
        {
            context.Set<CargoHabilidadEntity>().RemoveRange(
                await context.Set<CargoHabilidadEntity>().Where(ch => ch.CargoId == cargo1.Id || ch.CargoId == cargo2.Id)
                    .ToListAsync());
            context.Set<HabilidadEntity>().RemoveRange(habilidad, habilidad2);
            context.Set<CargoEntity>().RemoveRange(cargo1, cargo2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByCargoAndSkillAsync_NoExistente_RetornaNull()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var resultado = await repo.GetByCargoAndSkillAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [MySqlFact]
    public async Task ListByCargoIdAsync_SinAsignaciones_RetornaVacio()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoSkillRepository(context);

        var resultado = await repo.ListByCargoIdAsync(Guid.NewGuid(), default);

        Assert.Empty(resultado);
    }
}
