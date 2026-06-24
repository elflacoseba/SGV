using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Ocupaciones;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class OcupacionRepositoryTests
{
    // ── Active/History Queries ──────────────────────────────────

    [MySqlFact]
    public async Task ListAllAsync_Default_ReturnsOnlyActiveRows()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-LIST-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-LIST-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-LIST-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-LIST-PUE", unidad, cargo);

        var active = CreateOcupacion(persona.Id, puesto.Id, "OCUP-LIST-ACT", fechaFin: null);
        var finalized = CreateOcupacion(persona.Id, puesto.Id, "OCUP-LIST-FIN", fechaFin: new DateOnly(2024, 6, 30));
        var deleted = CreateOcupacion(persona.Id, puesto.Id, "OCUP-LIST-DEL", fechaFin: null, isDeleted: true);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, active, finalized, deleted);

            var repo = new OcupacionRepository(context);
            var result = await repo.ListAllAsync(default);

            Assert.All(result, o => Assert.IsType<Ocupacion>(o));
            Assert.Contains(result, o => o.Id == active.Id);
            Assert.DoesNotContain(result, o => o.Id == finalized.Id);
            Assert.DoesNotContain(result, o => o.Id == deleted.Id);
            Assert.All(result, o => Assert.True(o.EsVigente));
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, active, finalized, deleted);
        }
    }

    [MySqlFact]
    public async Task ListAllIncludingHistoryAsync_ReturnsAllRows()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-HIST-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-HIST-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-HIST-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-HIST-PUE", unidad, cargo);

        var active = CreateOcupacion(persona.Id, puesto.Id, "OCUP-HIST-ACT", fechaFin: null);
        var finalized = CreateOcupacion(persona.Id, puesto.Id, "OCUP-HIST-FIN", fechaFin: new DateOnly(2024, 6, 30));
        var deleted = CreateOcupacion(persona.Id, puesto.Id, "OCUP-HIST-DEL", fechaFin: null, isDeleted: true);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, active, finalized, deleted);

            var repo = new OcupacionRepository(context);
            var result = await repo.ListAllIncludingHistoryAsync(default);

            Assert.Contains(result, o => o.Id == active.Id);
            Assert.Contains(result, o => o.Id == finalized.Id);
            Assert.Contains(result, o => o.Id == deleted.Id);
            Assert.Equal(3, result.Count);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, active, finalized, deleted);
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_Active_ReturnsWithNavigation()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-FUPD-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-FUPD-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-FUPD-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-FUPD-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-FUPD-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var result = await repo.GetByIdForUpdateAsync(ocupacion.Id, default);

            Assert.NotNull(result);
            Assert.Equal(ocupacion.Id, result!.Id);
            Assert.NotNull(result.Persona);
            Assert.NotNull(result.Puesto);
            Assert.Equal(persona.Nombres, result.Persona.Nombres);
            Assert.Equal(puesto.Nombre, result.Puesto.Nombre);
            Assert.True(result.EsVigente);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_Finalized_ReturnsNull()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-FUP2-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-FUP2-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-FUP2-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-FUP2-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-FUP2-1", fechaFin: new DateOnly(2024, 6, 30));

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var result = await repo.GetByIdForUpdateAsync(ocupacion.Id, default);

            Assert.Null(result);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-GHIS-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-GHIS-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-GHIS-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-GHIS-PUE", unidad, cargo);
        var activo = CreateOcupacion(persona.Id, puesto.Id, "OCUP-GHIS-ACT", fechaFin: null);
        var eliminado = CreateOcupacion(persona.Id, puesto.Id, "OCUP-GHIS-DEL", fechaFin: null, isDeleted: true);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, activo, eliminado);

            var repo = new OcupacionRepository(context);
            var encontradoActivo = await repo.GetByIdIncludingHistoryAsync(activo.Id, default);
            var encontradoEliminado = await repo.GetByIdIncludingHistoryAsync(eliminado.Id, default);

            Assert.NotNull(encontradoActivo);
            Assert.Equal(activo.Id, encontradoActivo!.Id);
            Assert.True(encontradoActivo.EsVigente);

            Assert.NotNull(encontradoEliminado);
            Assert.Equal(eliminado.Id, encontradoEliminado!.Id);
            Assert.False(encontradoEliminado.EsVigente);
            Assert.True(encontradoEliminado.IsDeleted);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, activo, eliminado);
        }
    }

    // ── Soft-Delete/Reactivation Persistence ────────────────────

    [MySqlFact]
    public async Task UpdateAsync_WithSoftDelete_SavesIsDeleted()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-SDEL-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-SDEL-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-SDEL-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-SDEL-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-SDEL-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var loaded = await repo.GetByIdForUpdateAsync(ocupacion.Id, default);
            Assert.NotNull(loaded);

            loaded!.EliminarLogicamente();
            await repo.UpdateAsync(loaded, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<OcupacionEntity>()
                .FirstOrDefaultAsync(o => o.Id == ocupacion.Id);
            Assert.NotNull(entity);
            Assert.True(entity!.IsDeleted);
            Assert.NotNull(entity.DeletedAt);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_WithFinalize_SavesFechaFin()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-SFIN-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-SFIN-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-SFIN-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-SFIN-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-SFIN-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var loaded = await repo.GetByIdForUpdateAsync(ocupacion.Id, default);
            Assert.NotNull(loaded);

            loaded!.Finalizar(new DateOnly(2024, 12, 31));
            await repo.UpdateAsync(loaded, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<OcupacionEntity>()
                .FirstOrDefaultAsync(o => o.Id == ocupacion.Id);
            Assert.NotNull(entity);
            Assert.Equal(new DateOnly(2024, 12, 31), entity!.FechaFin);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-SREA-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-SREA-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-SREA-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-SREA-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-SREA-1",
            fechaFin: new DateOnly(2024, 6, 30), isDeleted: true);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var loaded = await repo.GetByIdIncludingHistoryAsync(ocupacion.Id, default);
            Assert.NotNull(loaded);

            loaded!.Reactivar();
            await repo.UpdateAsync(loaded, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<OcupacionEntity>()
                .FirstOrDefaultAsync(o => o.Id == ocupacion.Id);
            Assert.NotNull(entity);
            Assert.Null(entity!.FechaFin);
            Assert.False(entity.IsDeleted);
            Assert.Null(entity.DeletedAt);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    // ── Unique-Index Conflict Behavior ──────────────────────────

    [MySqlFact]
    public async Task ExistsActiveByPuestoAsync_Active_ReturnsTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-EPUE-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPUE-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPUE-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPUE-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-EPUE-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPuestoAsync(puesto.Id, cancellationToken: default);

            Assert.True(exists);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ExistsActiveByPuestoAsync_NoActive_ReturnsFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new OcupacionRepository(context);

        var exists = await repo.ExistsActiveByPuestoAsync(Guid.NewGuid(), cancellationToken: default);

        Assert.False(exists);
    }

    [MySqlFact]
    public async Task ExistsActiveByPuestoAsync_Finalized_ReturnsFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-EPU2-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPU2-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPU2-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPU2-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-EPU2-1", fechaFin: new DateOnly(2024, 6, 30));

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPuestoAsync(puesto.Id, cancellationToken: default);

            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-EPU3-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPU3-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPU3-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPU3-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-EPU3-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPuestoAsync(puesto.Id, ocupacion.Id, default);

            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-EPP-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPP-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPP-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPP-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-EPP-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPersonaYPuestoAsync(persona.Id, puesto.Id, cancellationToken: default);

            Assert.True(exists);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona1 = RepositoryTestData.CreatePersona("OCUP-EPP2-P1");
        var persona2 = RepositoryTestData.CreatePersona("OCUP-EPP2-P2");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPP2-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPP2-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPP2-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona1.Id, puesto.Id, "OCUP-EPP2-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona1, persona2, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPersonaYPuestoAsync(persona2.Id, puesto.Id, cancellationToken: default);

            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(context, persona1, persona2, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var persona = RepositoryTestData.CreatePersona("OCUP-EPP3-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-EPP3-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-EPP3-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-EPP3-PUE", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-EPP3-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);

            var repo = new OcupacionRepository(context);
            var exists = await repo.ExistsActiveByPersonaYPuestoAsync(persona.Id, puesto.Id, ocupacion.Id, default);

            Assert.False(exists);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static OcupacionEntity CreateOcupacion(
        Guid personaId, Guid puestoId, string prefix,
        DateOnly? fechaFin, bool isDeleted = false)
    {
        return new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = personaId,
            PuestoId = puestoId,
            FechaInicio = new DateOnly(2024, 1, 15),
            FechaFin = fechaFin,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = prefix,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task SeedAsync(SgvDbContext context, params object[] entities)
    {
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case PersonaEntity p:
                    context.Set<PersonaEntity>().Add(p);
                    break;
                case UnidadOrganizativaEntity u:
                    context.Set<UnidadOrganizativaEntity>().Add(u);
                    break;
                case CargoEntity c:
                    context.Set<CargoEntity>().Add(c);
                    break;
                case PuestoEntity p:
                    context.Set<PuestoEntity>().Add(p);
                    break;
                case OcupacionEntity o:
                    context.Set<OcupacionEntity>().Add(o);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task CleanupAsync(SgvDbContext context, params object[] entities)
    {
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case OcupacionEntity o:
                    context.Set<OcupacionEntity>().Remove(o);
                    break;
                case PuestoEntity p:
                    context.Set<PuestoEntity>().Remove(p);
                    break;
                case CargoEntity c:
                    context.Set<CargoEntity>().Remove(c);
                    break;
                case UnidadOrganizativaEntity u:
                    context.Set<UnidadOrganizativaEntity>().Remove(u);
                    break;
                case PersonaEntity p:
                    context.Set<PersonaEntity>().Remove(p);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }
}
