using Microsoft.EntityFrameworkCore;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Enums;
using SGV.Dominio.Vacantes;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Cobertura RED→GREEN de <see cref="VacanteRepository"/> sobre la base
/// MySQL de tests. Cubre los dos escenarios pivote del work unit 2.x:
/// segmentación sin mezclas (segmento=Abiertas excluye terminales) y
/// atomicidad transaccional de vacante + historial cuando
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> falla.
/// Los tests se skip-ean limpio cuando MySQL no está disponible
/// (<see cref="MySqlFactAttribute"/> + <see cref="MySqlTestDatabaseBootstrap"/>);
/// ver <c>openspec/changes/feature-implementar-modulo-vacantes/tasks.md</c>
/// Phase 2 (2.3, 2.4).
/// </summary>
public sealed class VacanteRepositoryQueryTests
{
    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    // ── 2.3 Segmentación sin mezcla ───────────────────────────────

    [MySqlFact]
    public async Task Segmento_Abiertas_ExcluyeTerminales()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-SEG-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-SEG-CARGO-{suffix}");
        // Cada vacante usa un puesto distinto: la constraint
        // IX_Vacantes_ActivePuestoIdUnique (issue #238) prohíbe dos
        // vacantes activas para el mismo PuestoId, así que el setup
        // necesita 4 puestos independientes. La invariante probada es
        // "segmento=Abiertas incluye solo estados no-terminales",
        // independientemente del puesto.
        var puesto1 = RepositoryTestData.CreatePuesto($"VAC-SEG-PUE1-{suffix}", unidad, cargo);
        var puesto2 = RepositoryTestData.CreatePuesto($"VAC-SEG-PUE2-{suffix}", unidad, cargo);
        var puesto3 = RepositoryTestData.CreatePuesto($"VAC-SEG-PUE3-{suffix}", unidad, cargo);
        var puesto4 = RepositoryTestData.CreatePuesto($"VAC-SEG-PUE4-{suffix}", unidad, cargo);
        var abierta = CrearEstadoVacante($"VAC-SEG-EST-ABIERTA-{suffix}", "Abierta", orden: 1, esTerminal: false);
        var enSeleccion = CrearEstadoVacante($"VAC-SEG-EST-ENSEL-{suffix}", "EnSeleccion", orden: 2, esTerminal: false);
        var cubierta = CrearEstadoVacante($"VAC-SEG-EST-CUBIERTA-{suffix}", "Cubierta", orden: 3, esTerminal: true);
        var cancelada = CrearEstadoVacante($"VAC-SEG-EST-CANCEL-{suffix}", "Cancelada", orden: 4, esTerminal: true);

        var vacAbierta1 = CrearVacante(puesto1.Id, abierta.Id, $"VAC-SEG-VAC-AB1-{suffix}");
        var vacAbierta2 = CrearVacante(puesto2.Id, enSeleccion.Id, $"VAC-SEG-VAC-AB2-{suffix}");
        var vacCubierta = CrearVacante(puesto3.Id, cubierta.Id, $"VAC-SEG-VAC-CUB-{suffix}");
        var vacCancelada = CrearVacante(puesto4.Id, cancelada.Id, $"VAC-SEG-VAC-CAN-{suffix}");

        try
        {
            await SeedAsync(context, unidad, cargo,
                puesto1, puesto2, puesto3, puesto4,
                abierta, enSeleccion, cubierta, cancelada,
                vacAbierta1, vacAbierta2, vacCubierta, vacCancelada);

            var repo = new VacanteRepository(context);
            var query = new VacanteListQuery(
                Page: 1,
                PageSize: 20,
                Search: null,
                Sort: null,
                Segmento: VacanteSegmentoListado.Abiertas);

            var (items, totalCount) = await repo.ListarAsync(query, default);

            // Solo deben aparecer las dos vacantes en estados no terminales
            // (Abierta + EnSeleccion). Las terminales (Cubierta, Cancelada)
            // NO deben mezclarse en el segmento Abiertas.
            Assert.Equal(2, totalCount);
            Assert.Equal(2, items.Count);
            Assert.Contains(items, v => v.Id == vacAbierta1.Id);
            Assert.Contains(items, v => v.Id == vacAbierta2.Id);
            Assert.DoesNotContain(items, v => v.Id == vacCubierta.Id);
            Assert.DoesNotContain(items, v => v.Id == vacCancelada.Id);
        }
        finally
        {
            await CleanupAsync(context, vacAbierta1, vacAbierta2, vacCubierta, vacCancelada,
                abierta, enSeleccion, cubierta, cancelada,
                puesto1, puesto2, puesto3, puesto4, cargo, unidad);
        }
    }

    [MySqlFact]
    public async Task Segmento_Cerradas_ExcluyeAbiertas()
    {
        // Caso hermano del pivote: Cerradas debe contener solo las terminales.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-CER-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-CER-CARGO-{suffix}");
        // Cada vacante usa un puesto distinto para satisfacer la constraint
        // IX_Vacantes_ActivePuestoIdUnique (issue #238) — la fórmula
        // ActivePuestoIdUnique depende de FechaCierre/IsDeleted y no del
        // nombre del estado, así que vacAbierta y vacCubierta computarían
        // ambas al mismo PuestoId.
        var puesto1 = RepositoryTestData.CreatePuesto($"VAC-CER-PUE1-{suffix}", unidad, cargo);
        var puesto2 = RepositoryTestData.CreatePuesto($"VAC-CER-PUE2-{suffix}", unidad, cargo);
        var abierta = CrearEstadoVacante($"VAC-CER-AB-{suffix}", "Abierta", orden: 1, esTerminal: false);
        var cubierta = CrearEstadoVacante($"VAC-CER-CUB-{suffix}", "Cubierta", orden: 2, esTerminal: true);

        var vacAbierta = CrearVacante(puesto1.Id, abierta.Id, $"VAC-CER-VAC-AB-{suffix}");
        var vacCubierta = CrearVacante(puesto2.Id, cubierta.Id, $"VAC-CER-VAC-CUB-{suffix}");

        try
        {
            await SeedAsync(context, unidad, cargo, puesto1, puesto2, abierta, cubierta, vacAbierta, vacCubierta);

            var repo = new VacanteRepository(context);
            var query = new VacanteListQuery(1, 20, null, null, VacanteSegmentoListado.Cerradas);

            var (items, totalCount) = await repo.ListarAsync(query, default);

            Assert.Equal(1, totalCount);
            Assert.Single(items);
            Assert.Equal(vacCubierta.Id, items[0].Id);
            Assert.DoesNotContain(items, v => v.Id == vacAbierta.Id);
        }
        finally
        {
            await CleanupAsync(context, vacAbierta, vacCubierta, abierta, cubierta, puesto1, puesto2, cargo, unidad);
        }
    }

    // ── 2.4 Atomicidad vacante + historial ─────────────────────────

    [MySqlFact]
    public async Task CambiarEstado_AtomicidadVacanteEHistorial()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-ATOM-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-ATOM-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"VAC-ATOM-PUE-{suffix}", unidad, cargo);
        var estadoAbierta = CrearEstadoVacante($"VAC-ATOM-EST-{suffix}", "Abierta", orden: 1, esTerminal: false);

        var vacante = CrearVacante(puesto.Id, estadoAbierta.Id, $"VAC-ATOM-VAC-{suffix}");

        try
        {
            await SeedAsync(context, unidad, cargo, puesto, estadoAbierta, vacante);

            var repo = new VacanteRepository(context);
            var vacanteDomain = await repo.GetByIdForUpdateAsync(vacante.Id, default);
            Assert.NotNull(vacanteDomain);
            Assert.Equal(estadoAbierta.Id, vacanteDomain!.EstadoVacanteId);

            // Forzar una falla de FK a nivel de SaveChangesAsync: cambiar
            // EstadoVacanteId a un Guid inexistente y agregar simultáneamente
            // un HistorialEstadoVacanteEntity que también referencia ese Guid.
            // Ambas mutaciones viven en el mismo SaveChangesAsync → misma
            // transacción → si una falla, ambas deben revertir.
            var entity = await context.Set<VacanteEntity>()
                .Include(v => v.HistorialEstados)
                .FirstAsync(v => v.Id == vacante.Id);

            var invalidEstadoId = Guid.NewGuid(); // FK violation esperada
            entity.EstadoVacanteId = invalidEstadoId;
            entity.HistorialEstados.Add(new HistorialEstadoVacanteEntity
            {
                Id = Guid.NewGuid(),
                EstadoAnteriorId = estadoAbierta.Id,
                EstadoNuevoId = invalidEstadoId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = "test-user",
                Motivo = "Atomicidad test"
            });

            // La transacción debe revertir AMBAS mutaciones (vacante.EstadoVacanteId
            // + nuevo historial) cuando la FK del historial falla.
            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());

            // Releer el estado de la base con un context fresco y AsNoTracking
            // para confirmar que la base de datos no persistió ninguno de los
            // dos cambios.
            await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
            var entityDespues = await verifyContext.Set<VacanteEntity>()
                .AsNoTracking()
                .Include(v => v.HistorialEstados)
                .FirstAsync(v => v.Id == vacante.Id);

            Assert.Equal(estadoAbierta.Id, entityDespues.EstadoVacanteId);
            Assert.Empty(entityDespues.HistorialEstados);
        }
        finally
        {
            await CleanupAsync(context, vacante, estadoAbierta, puesto, cargo, unidad);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static EstadoVacanteEntity CrearEstadoVacante(string prefix, string codigo, int orden, bool esTerminal)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"{codigo}-{suffix}",
            Nombre = $"{prefix} {suffix}",
            Orden = orden,
            EsTerminal = esTerminal
        };
    }

    private static VacanteEntity CrearVacante(Guid puestoId, Guid estadoVacanteId, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puestoId,
            EstadoVacanteId = estadoVacanteId,
            FechaApertura = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"{prefix} {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task SeedAsync(SgvDbContext context, params object[] entities)
    {
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case UnidadOrganizativaEntity u:
                    await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
                    break;
                case CargoEntity c:
                    await context.Set<CargoEntity>().AddAsync(c);
                    break;
                case PuestoEntity p:
                    await context.Set<PuestoEntity>().AddAsync(p);
                    break;
                case EstadoVacanteEntity e:
                    await context.Set<EstadoVacanteEntity>().AddAsync(e);
                    break;
                case VacanteEntity v:
                    await context.Set<VacanteEntity>().AddAsync(v);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task CleanupAsync(SgvDbContext context, params object[] entities)
    {
        // Borrar en orden topológico: dependientes primero, principales al final.
        // Sin esto, EF lanza "association severed" al intentar remover un
        // Puesto cuya Vacante (FK RESTRICT) sigue trackeada.
        var ordered = entities.OrderBy(e => e switch
        {
            VacanteEntity => 0,
            EstadoVacanteEntity => 1,
            PuestoEntity => 2,
            CargoEntity => 3,
            UnidadOrganizativaEntity => 4,
            _ => 99,
        });

        foreach (var entity in ordered)
        {
            switch (entity)
            {
                case VacanteEntity v:
                    context.Set<VacanteEntity>().Remove(v);
                    break;
                case EstadoVacanteEntity e:
                    context.Set<EstadoVacanteEntity>().Remove(e);
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
            }
        }

        await context.SaveChangesAsync();
    }
}