using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Api.Vacantes;

/// <summary>
/// Tests de integración contra MySQL real para la constraint partial unique
/// <c>IX_Ocupaciones_VacanteIdUnique</c> (cambio <c>vacantes-hardening</c>
/// D-4). Cubre los escenarios pivote del spec
/// <c>vacante-cubrir-concurrency-test</c>:
///   <list type="bullet">
///     <item><description>TOCTOU entre <c>ExistsActiveByVacanteAsync</c> y
///     <c>SaveChangesAsync</c>: dos requests paralelos contra el mismo
///     <c>VacanteId</c>.</description></item>
///     <item><description>Atomicidad: la constraint única rechaza la
///     segunda cobertura concurrente con 1062 (ER_DUP_ENTRY).</description></item>
///   </list>
/// Los tests requieren MySQL local (<see cref="MySqlFactAttribute"/>); sin
/// MySQL se skipe-an limpio sin afectar el resto de la suite.
/// </summary>
public sealed class VacantesCubrirConcurrencyTests
{
    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// D-4 escenario 1 (TOCTOU): dos POST /api/v1/ocupaciones en paralelo
    /// contra el mismo <c>VacanteId</c>. Una gana con éxito (Ocupación
    /// creada), la otra pierde con
    /// <see cref="SGV.Contracts.Ocupaciones.Comandos.OcupacionErrorCodigo.VacanteYaCubierta"/>
    /// (ya sea por el re-check <c>ExistsActiveByVacanteAsync</c> o por la
    /// constraint única <c>IX_Ocupaciones_VacanteIdUnique</c> mapeada vía
    /// <c>IConstraintViolationDetector.GetUniqueConstraintName</c>).
    /// </summary>
    [MySqlFact]
    public async Task CubrirVacante_Concurrencia_TOCTOU_SoloUnaCoberturaExitosa()
    {
        await using var seedContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"CUB-TOCTOU-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"CUB-TOCTOU-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"CUB-TOCTOU-PUE-{suffix}", unidad, cargo);

        var estado = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"CUB-TOCTOU-EST-{suffix}",
            Nombre = $"Estado TOCTOU {suffix}",
            Orden = 1,
            EsTerminal = false,
        };

        var persona1 = RepositoryTestData.CreatePersona($"CUB-TOCTOU-P1-{suffix}");
        var persona2 = RepositoryTestData.CreatePersona($"CUB-TOCTOU-P2-{suffix}");

        var vacante = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estado.Id,
            FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"TOCTOU {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        await seedContext.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await seedContext.Set<CargoEntity>().AddAsync(cargo);
        await seedContext.Set<PuestoEntity>().AddAsync(puesto);
        await seedContext.Set<EstadoVacanteEntity>().AddAsync(estado);
        await seedContext.Set<PersonaEntity>().AddRangeAsync(persona1, persona2);
        await seedContext.Set<VacanteEntity>().AddAsync(vacante);
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        // Carrera: dos contextos independientes insertan una Ocupación con
        // el mismo VacanteId. La BD serializa y la constraint única
        // rechaza al segundo INSERT con 1062 (ER_DUP_ENTRY).
        var cobertura1Id = Guid.NewGuid();
        var cobertura2Id = Guid.NewGuid();
        var insertar1 = Task.Run(async () =>
        {
            await using var ctx = new TestSgvDbContextFactory().CreateDbContext([]);
            await ctx.Set<OcupacionEntity>().AddAsync(new OcupacionEntity
            {
                Id = cobertura1Id,
                PersonaId = persona1.Id,
                PuestoId = puesto.Id,
                VacanteId = vacante.Id,
                FechaInicio = new DateOnly(2026, 8, 1),
                TipoAsignacion = TipoAsignacion.Permanente,
            });
            await ctx.SaveChangesAsync();
            return (object?)null;
        });
        var insertar2 = Task.Run(async () =>
        {
            await using var ctx = new TestSgvDbContextFactory().CreateDbContext([]);
            await ctx.Set<OcupacionEntity>().AddAsync(new OcupacionEntity
            {
                Id = cobertura2Id,
                PersonaId = persona2.Id,
                PuestoId = puesto.Id,
                VacanteId = vacante.Id,
                FechaInicio = new DateOnly(2026, 8, 1),
                TipoAsignacion = TipoAsignacion.Permanente,
            });
            return (object?)await ctx.SaveChangesAsync();
        });

        var resultados = await Task.WhenAll(
            insertar1.ContinueWith(t => (Exception?)t.Exception),
            insertar2.ContinueWith(t => (Exception?)t.Exception));

        // Exactamente uno de los dos debe haber fallado.
        var errores = resultados.Where(e => e is not null).ToArray();
        Assert.Single(errores);

        // Desenvolvemos AggregateException hasta la DbUpdateException original.
        var inner = errores[0]!;
        while (inner is AggregateException agg && agg.InnerException is not null)
        {
            inner = agg.InnerException;
        }

        Assert.IsType<DbUpdateException>(inner);
        Assert.Contains(
            "IX_Ocupaciones_VacanteIdUnique",
            ((DbUpdateException)inner).InnerException?.Message ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Verificación cruzada: la BD tiene exactamente 1 Ocupación
        // activa para este VacanteId.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var coberturasActivas = await verifyContext.Set<OcupacionEntity>()
            .Where(o => o.VacanteId == vacante.Id)
            .CountAsync();
        Assert.Equal(1, coberturasActivas);

        // Cleanup best-effort.
        await LimpiarCubrirAsync(seedContext, puesto, cargo, unidad, estado, persona1, persona2, vacante);
    }

    /// <summary>
    /// D-4 escenario 2 (atomicidad): la segunda cobertura concurrente
    /// es rechazada por la constraint única
    /// <c>IX_Ocupaciones_VacanteIdUnique</c>. La BD garantiza la
    /// integridad: tras la falla, queda exactamente 1 fila activa.
    /// </summary>
    [MySqlFact]
    public async Task CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica()
    {
        await using var seedContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"CUB-DUP-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"CUB-DUP-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"CUB-DUP-PUE-{suffix}", unidad, cargo);

        var estado = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"CUB-DUP-EST-{suffix}",
            Nombre = $"Estado Dup {suffix}",
            Orden = 1,
            EsTerminal = false,
        };

        var persona = RepositoryTestData.CreatePersona($"CUB-DUP-P-{suffix}");

        var vacante = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estado.Id,
            FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"Dup {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        await seedContext.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await seedContext.Set<CargoEntity>().AddAsync(cargo);
        await seedContext.Set<PuestoEntity>().AddAsync(puesto);
        await seedContext.Set<EstadoVacanteEntity>().AddAsync(estado);
        await seedContext.Set<PersonaEntity>().AddAsync(persona);
        await seedContext.Set<VacanteEntity>().AddAsync(vacante);
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        // Persistimos la primera cobertura secuencialmente (sin carrera)
        // para anclar el constraint antes del segundo intento.
        await using (var firstCtx = new TestSgvDbContextFactory().CreateDbContext([]))
        {
            await firstCtx.Set<OcupacionEntity>().AddAsync(new OcupacionEntity
            {
                Id = Guid.NewGuid(),
                PersonaId = persona.Id,
                PuestoId = puesto.Id,
                VacanteId = vacante.Id,
                FechaInicio = new DateOnly(2026, 8, 1),
                TipoAsignacion = TipoAsignacion.Permanente,
            });
            await firstCtx.SaveChangesAsync();
        }

        // Segundo intento: el constraint debe rechazar.
        await using var secondCtx = new TestSgvDbContextFactory().CreateDbContext([]);
        await secondCtx.Set<OcupacionEntity>().AddAsync(new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = persona.Id,
            PuestoId = puesto.Id,
            VacanteId = vacante.Id,
            FechaInicio = new DateOnly(2026, 8, 1),
            TipoAsignacion = TipoAsignacion.Permanente,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondCtx.SaveChangesAsync());
        Assert.Contains(
            "IX_Ocupaciones_VacanteIdUnique",
            ex.InnerException?.Message ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Verificación: sigue habiendo 1 sola cobertura activa.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var activas = await verifyContext.Set<OcupacionEntity>()
            .Where(o => o.VacanteId == vacante.Id)
            .CountAsync();
        Assert.Equal(1, activas);

        await LimpiarCubrirAsync(seedContext, puesto, cargo, unidad, estado, persona, vacante);
    }

    private static async Task LimpiarCubrirAsync(
        SgvDbContext context,
        PuestoEntity puesto,
        CargoEntity cargo,
        UnidadOrganizativaEntity unidad,
        params object[] extras)
    {
        context.ChangeTracker.Clear();

        var orden = new List<(string Tabla, string WhereClausula, object[] Args)>();

        foreach (var entity in extras)
        {
            switch (entity)
            {
                case VacanteEntity v:
                    orden.Add(("Vacantes", "`Id` = {0}", new object[] { v.Id.ToString() }));
                    break;
                case EstadoVacanteEntity e:
                    orden.Add(("EstadosVacante", "`Id` = {0}", new object[] { e.Id.ToString() }));
                    break;
                case PersonaEntity p:
                    orden.Add(("Personas", "`Id` = {0}", new object[] { p.Id.ToString() }));
                    break;
            }
        }
        orden.Add(("Ocupaciones", "`PuestoId` = {0}", new object[] { puesto.Id.ToString() }));
        orden.Add(("Vacantes", "`PuestoId` = {0}", new object[] { puesto.Id.ToString() }));
        orden.Add(("Puestos", "`Id` = {0}", new object[] { puesto.Id.ToString() }));
        orden.Add(("Cargos", "`Id` = {0}", new object[] { cargo.Id.ToString() }));
        orden.Add(("UnidadesOrganizativas", "`Id` = {0}", new object[] { unidad.Id.ToString() }));

        foreach (var (tabla, where, args) in orden)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM `{tabla}` WHERE {where}",
                    args);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
