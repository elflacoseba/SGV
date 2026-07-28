using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Ocupaciones;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Espejo de <c>CargoRepositoryTests.QueryAsync_MySql_*</c>; cubre
/// <see cref="OcupacionRepository.QueryAsync(OcupacionListQuery, CancellationToken)"/>
/// con su nuevo contrato (segmento + filtros contextuales + búsqueda + sort + paginación).
/// Los tests se skip-ean limpio sin MySQL local — ver <c>openspec/changes/2026-07-28-web-ocupaciones-issue-208</c>.
/// </summary>
public sealed class OcupacionRepositoryQueryAsyncTests
{
    private static readonly Guid PersonaA = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonaB = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid PuestoX = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid PuestoY = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminadasYFinalizadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var persona = RepositoryTestData.CreatePersona($"OCUP-QRY-ELI-PER-{suffix}");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCUP-QRY-ELI-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCUP-QRY-ELI-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"OCUP-QRY-ELI-PUE-{suffix}", unidad, cargo);
        var activa = CreateOcupacion(persona.Id, puesto.Id, "OCUP-QRY-ELI-ACT", fechaFin: null);
        var finalizada = CreateOcupacion(persona.Id, puesto.Id, "OCUP-QRY-ELI-FIN", fechaFin: new DateOnly(2024, 6, 30));
        var eliminada = CreateOcupacion(persona.Id, puesto.Id, "OCUP-QRY-ELI-DEL", fechaFin: null, isDeleted: true);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, activa, finalizada, eliminada);
            var repo = new OcupacionRepository(context);
            var result = await repo.QueryAsync(
                new OcupacionListQuery(1, 20, null, null, OcupacionSegmentoListado.Eliminadas),
                default);

            Assert.Equal(2, result.TotalCount);
            Assert.Contains(result.Items, o => o.Id == finalizada.Id);
            Assert.Contains(result.Items, o => o.Id == eliminada.Id);
            Assert.DoesNotContain(result.Items, o => o.Id == activa.Id);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, activa, finalizada, eliminada);
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_FiltroPorPersonaId_RetornaSoloCoincidencias()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var personaA = RepositoryTestData.CreatePersona($"OCUP-QRY-FP-A-{suffix}");
        var personaB = RepositoryTestData.CreatePersona($"OCUP-QRY-FP-B-{suffix}");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCUP-QRY-FP-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCUP-QRY-FP-CARGO-{suffix}");
        var puestoX = RepositoryTestData.CreatePuesto($"OCUP-QRY-FP-PUE-X-{suffix}", unidad, cargo);
        var puestoY = RepositoryTestData.CreatePuesto($"OCUP-QRY-FP-PUE-Y-{suffix}", unidad, cargo);
        var ocupacionA = CreateOcupacion(personaA.Id, puestoX.Id, "OCUP-QRY-FP-1", fechaFin: null);
        var ocupacionB = CreateOcupacion(personaB.Id, puestoY.Id, "OCUP-QRY-FP-2", fechaFin: null);

        try
        {
            await SeedAsync(context, personaA, personaB, unidad, cargo, puestoX, puestoY, ocupacionA, ocupacionB);
            var repo = new OcupacionRepository(context);
            var result = await repo.QueryAsync(
                new OcupacionListQuery(1, 20, null, null, OcupacionSegmentoListado.Activas, PersonaId: personaA.Id),
                default);

            Assert.Single(result.Items);
            Assert.Equal(ocupacionA.Id, result.Items[0].Id);
        }
        finally
        {
            await CleanupAsync(context, personaA, personaB, unidad, cargo, puestoX, puestoY, ocupacionA, ocupacionB);
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_FiltroPorPuestoId_RetornaSoloCoincidencias()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var persona = RepositoryTestData.CreatePersona($"OCUP-QRY-FPU-PER-{suffix}");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCUP-QRY-FPU-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCUP-QRY-FPU-CARGO-{suffix}");
        var puestoX = RepositoryTestData.CreatePuesto($"OCUP-QRY-FPU-X-{suffix}", unidad, cargo);
        var puestoY = RepositoryTestData.CreatePuesto($"OCUP-QRY-FPU-Y-{suffix}", unidad, cargo);
        var occX = CreateOcupacion(persona.Id, puestoX.Id, "OCUP-QRY-FPU-1", fechaFin: null);
        var occY = CreateOcupacion(persona.Id, puestoY.Id, "OCUP-QRY-FPU-2", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puestoX, puestoY, occX, occY);
            var repo = new OcupacionRepository(context);
            var result = await repo.QueryAsync(
                new OcupacionListQuery(1, 20, null, null, OcupacionSegmentoListado.Activas, PuestoId: puestoX.Id),
                default);

            Assert.Single(result.Items);
            Assert.Equal(occX.Id, result.Items[0].Id);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puestoX, puestoY, occX, occY);
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_FiltrosCombinadosSinCoincidencia_RetornaColeccionVaciaYTotalCero()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var persona = RepositoryTestData.CreatePersona($"OCUP-QRY-CMB-PER-{suffix}");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCUP-QRY-CMB-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCUP-QRY-CMB-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"OCUP-QRY-CMB-PUE-{suffix}", unidad, cargo);
        var ocupacion = CreateOcupacion(persona.Id, puesto.Id, "OCUP-QRY-CMB-1", fechaFin: null);

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puesto, ocupacion);
            var repo = new OcupacionRepository(context);
            var result = await repo.QueryAsync(
                new OcupacionListQuery(1, 20, null, null, OcupacionSegmentoListado.Activas,
                    PersonaId: Guid.NewGuid(), PuestoId: Guid.NewGuid()),
                default);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_Paginacion_TotalCountReflejaFiltros()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var persona = RepositoryTestData.CreatePersona($"OCUP-QRY-PAG-PER-{suffix}");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCUP-QRY-PAG-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCUP-QRY-PAG-CARGO-{suffix}");
        var puestoX = RepositoryTestData.CreatePuesto($"OCUP-QRY-PAG-PUE-X-{suffix}", unidad, cargo);
        var puestoY = RepositoryTestData.CreatePuesto($"OCUP-QRY-PAG-PUE-Y-{suffix}", unidad, cargo);
        var puestoZ = RepositoryTestData.CreatePuesto($"OCUP-QRY-PAG-PUE-Z-{suffix}", unidad, cargo);
        var o1 = CreateOcupacion(persona.Id, puestoX.Id, "OCUP-QRY-PAG-1", fechaFin: null, fechaInicio: new DateOnly(2024, 1, 1));
        var o2 = CreateOcupacion(persona.Id, puestoY.Id, "OCUP-QRY-PAG-2", fechaFin: null, fechaInicio: new DateOnly(2024, 2, 1));
        var o3 = CreateOcupacion(persona.Id, puestoZ.Id, "OCUP-QRY-PAG-3", fechaFin: null, fechaInicio: new DateOnly(2024, 3, 1));

        try
        {
            await SeedAsync(context, persona, unidad, cargo, puestoX, puestoY, puestoZ, o1, o2, o3);
            var repo = new OcupacionRepository(context);
            var result = await repo.QueryAsync(
                new OcupacionListQuery(2, 1, null, null, OcupacionSegmentoListado.Activas,
                    PersonaId: persona.Id),
                default);

            Assert.Equal(3, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(o2.Id, result.Items[0].Id);
        }
        finally
        {
            await CleanupAsync(context, persona, unidad, cargo, puestoX, puestoY, puestoZ, o1, o2, o3);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static OcupacionEntity CreateOcupacion(
        Guid personaId, Guid puestoId, string prefix,
        DateOnly? fechaFin, bool isDeleted = false,
        DateOnly? fechaInicio = null)
    {
        return new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = personaId,
            PuestoId = puestoId,
            FechaInicio = fechaInicio ?? new DateOnly(2024, 1, 15),
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
                    await context.Set<PersonaEntity>().AddAsync(p);
                    break;
                case UnidadOrganizativaEntity u:
                    await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
                    break;
                case CargoEntity c:
                    await context.Set<CargoEntity>().AddAsync(c);
                    break;
                case PuestoEntity p:
                    await context.Set<PuestoEntity>().AddAsync(p);
                    break;
                case OcupacionEntity o:
                    await context.Set<OcupacionEntity>().AddAsync(o);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task CleanupAsync(SgvDbContext context, params object[] entities)
    {
        var ordered = entities.OrderBy(e => e switch
        {
            OcupacionEntity => 0,
            PuestoEntity => 1,
            CargoEntity => 2,
            UnidadOrganizativaEntity => 3,
            PersonaEntity => 4,
            _ => 99,
        });

        foreach (var entity in ordered)
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
