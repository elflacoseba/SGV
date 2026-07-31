using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Api.Vacantes;

/// <summary>
/// Tests de integración contra MySQL real para la constraint partial unique
/// <c>IX_Vacantes_ActivePuestoIdUnique</c> (issue #238, D-3.2 del change
/// archivado). Cubre los escenarios pivote del spec
/// <c>vacante-management</c>:
///   <list type="bullet">
///     <item><description>Carrera concurrente para el mismo PuestoId —
///     el índice único rechaza la segunda inserción.</description></item>
///     <item><description>Liberar al cerrar: vacante cerrada no viola
///     la constraint — se puede abrir una nueva para el mismo Puesto.</description></item>
///     <item><description>Soft-delete también libera el índice — la
///     columna calculada evalúa a NULL también para <c>IsDeleted = 1</c>.</description></item>
///   </list>
/// Los tests requieren MySQL local (<see cref="MySqlFactAttribute"/>); sin
/// MySQL se skipe-an limpio sin afectar el resto de la suite.
/// </summary>
public sealed class VacantesConcurrenciaTests
{
    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// T7.1.a: carrera concurrente para el mismo <c>PuestoId</c>. Dos
    /// contextos EF separados, cada uno con su propia transacción,
    /// insertan en paralelo contra la misma fila de catálogo. La BD
    /// serializa los INSERTs y la constraint
    /// <c>IX_Vacantes_ActivePuestoIdUnique</c> rechaza la segunda
    /// inserción con 1062 (ER_DUP_ENTRY). Esta es la garantía que el fix
    /// provee: la BD es la fuente de verdad final ante TOCTOU entre el
    /// pre-check <c>ExistsAbiertaByPuestoAsync</c> y el
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    [MySqlFact]
    public async Task Crear_MismoPuestoIdConcurrente_UnoPersisteOtroFallaConDbUpdateException()
    {
        // Setup compartido en un context semilla.
        await using var seedContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-CONC-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-CONC-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"VAC-CONC-PUE-{suffix}", unidad, cargo);

        var estado = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"VAC-CONC-EST-{suffix}",
            Nombre = $"Estado Vacante Carrera {suffix}",
            Orden = 1,
            EsTerminal = false,
        };

        await seedContext.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await seedContext.Set<CargoEntity>().AddAsync(cargo);
        await seedContext.Set<PuestoEntity>().AddAsync(puesto);
        await seedContext.Set<EstadoVacanteEntity>().AddAsync(estado);
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        // Carrera: dos contextos independientes, dos transacciones,
        // mismo PuestoId.
        var vacante1Id = Guid.NewGuid();
        var vacante2Id = Guid.NewGuid();
        var insertar1 = Task.Run(async () =>
        {
            await using var ctx1 = new TestSgvDbContextFactory().CreateDbContext([]);
            await ctx1.Set<VacanteEntity>().AddAsync(new VacanteEntity
            {
                Id = vacante1Id,
                PuestoId = puesto.Id,
                EstadoVacanteId = estado.Id,
                FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                Motivo = $"Carrera 1 {suffix}",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx1.SaveChangesAsync();
            return (object?)null;
        });
        var insertar2 = Task.Run(async () =>
        {
            await using var ctx2 = new TestSgvDbContextFactory().CreateDbContext([]);
            await ctx2.Set<VacanteEntity>().AddAsync(new VacanteEntity
            {
                Id = vacante2Id,
                PuestoId = puesto.Id,
                EstadoVacanteId = estado.Id,
                FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                Motivo = $"Carrera 2 {suffix}",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
            });
            return (object?)await ctx2.SaveChangesAsync();
        });

        var resultados = await Task.WhenAll(
            insertar1.ContinueWith(t => (Exception?)t.Exception),
            insertar2.ContinueWith(t => (Exception?)t.Exception));

        // Exactamente uno de los dos debe haber fallado con
        // DbUpdateException y el otro haber persistido.
        var errores = resultados.Where(e => e is not null).ToArray();
        Assert.Single(errores);

        // Task.Exception viene envuelto en AggregateException, así que
        // desenvolvemos hasta llegar a la DbUpdateException original.
        var inner = errores[0]!;
        while (inner is AggregateException agg && agg.InnerException is not null)
        {
            inner = agg.InnerException;
        }

        Assert.IsType<DbUpdateException>(inner);
        Assert.Contains(
            "IX_Vacantes_ActivePuestoIdUnique",
            ((DbUpdateException)inner).InnerException?.Message ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Verificación cruzada: la constraint dejó exactamente 1 fila
        // activa para este PuestoId en la BD.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var abiertasRestantes = await verifyContext.Set<VacanteEntity>()
            .Where(v => v.PuestoId == puesto.Id
                && v.FechaCierre == null
                && !v.IsDeleted)
            .CountAsync();
        Assert.Equal(1, abiertasRestantes);

        // Cleanup best-effort via SQL raw.
        await seedContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM `Vacantes` WHERE `PuestoId` = {0}",
            puesto.Id.ToString());
        await LimpiarVacantesAsync(seedContext, puesto, cargo, unidad, estado);
    }

    /// <summary>
    /// T7.1.b: cerrar la vacante (estado terminal → <c>FechaCierre</c>
    /// seteada) libera el índice: una nueva vacante para el mismo
    /// <c>PuestoId</c> se persiste sin violar la constraint. La columna
    /// calculada evalúa a <c>NULL</c> para vacantes cerradas, y MySQL
    /// ignora <c>NULL</c> en el unique index.
    /// </summary>
    [MySqlFact]
    public async Task CerrarYReabrir_VacanteNuevaParaMismoPuesto_NoViolaConstraint()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-REL-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-REL-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"VAC-REL-PUE-{suffix}", unidad, cargo);

        var estadoAbierta = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"VAC-REL-AB-{suffix}",
            Nombre = $"Abierta {suffix}",
            Orden = 1,
            EsTerminal = false,
        };
        var estadoTerminal = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"VAC-REL-CUB-{suffix}",
            Nombre = $"Cubierta {suffix}",
            Orden = 2,
            EsTerminal = true,
        };

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puesto);
        await context.Set<EstadoVacanteEntity>().AddRangeAsync(estadoAbierta, estadoTerminal);
        await context.SaveChangesAsync();

        var primera = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estadoAbierta.Id,
            FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"Primera {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        await context.Set<VacanteEntity>().AddAsync(primera);
        await context.SaveChangesAsync();

        // Cerrar la primera: setear FechaCierre directamente simula un
        // cambio a estado terminal. La columna calculada pasa a NULL.
        var firstTracked = await context.Set<VacanteEntity>()
            .FirstAsync(v => v.Id == primera.Id);
        firstTracked.FechaCierre = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        firstTracked.EstadoVacanteId = estadoTerminal.Id;
        await context.SaveChangesAsync();

        Assert.NotNull(primera.FechaCierre);

        // Crear una nueva para el mismo PuestoId. La columna calculada de
        // la primera ahora evalúa a NULL (cerrada), por lo que la
        // constraint no choca con la nueva.
        var segunda = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estadoAbierta.Id,
            FechaApertura = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"Segunda {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        await context.Set<VacanteEntity>().AddAsync(segunda);
        await context.SaveChangesAsync(); // NO debe lanzar

        // Verificación cruzada: la BD tiene la cerrada (FechaCierre != null)
        // y la nueva (FechaCierre == null) coexistiendo para el mismo Puesto.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var abiertas = await verifyContext.Set<VacanteEntity>()
            .Where(v => v.PuestoId == puesto.Id
                && v.FechaCierre == null
                && !v.IsDeleted)
            .CountAsync();
        var cerradas = await verifyContext.Set<VacanteEntity>()
            .Where(v => v.PuestoId == puesto.Id && v.FechaCierre != null)
            .CountAsync();

        Assert.Equal(1, abiertas);
        Assert.Equal(1, cerradas);

        await LimpiarVacantesAsync(context, puesto, cargo, unidad, estadoAbierta, estadoTerminal, primera, segunda);
    }

    /// <summary>
    /// T7.1.c (bonus — soft-delete libera la vacante activa). Soft-delete
    /// (<c>IsDeleted = 1</c>) hace que la columna calculada evalúe a
    /// <c>NULL</c>, igual que <c>FechaCierre != NULL</c>. Esta es la otra
    /// rama del <c>CASE WHEN FechaCierre IS NULL AND IsDeleted = 0</c>.
    /// </summary>
    [MySqlFact]
    public async Task SoftDeleteLiberaIndice_NuevaParaMismoPuesto_NoViolaConstraint()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"VAC-SD-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"VAC-SD-CARGO-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"VAC-SD-PUE-{suffix}", unidad, cargo);
        var estado = new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"VAC-SD-EST-{suffix}",
            Nombre = $"Estado {suffix}",
            Orden = 1,
            EsTerminal = false,
        };

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puesto);
        await context.Set<EstadoVacanteEntity>().AddAsync(estado);
        await context.SaveChangesAsync();

        var primera = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estado.Id,
            FechaApertura = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"Primera {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        await context.Set<VacanteEntity>().AddAsync(primera);
        await context.SaveChangesAsync();

        // Soft-delete a nivel de BD (igual que PuestoRepository.DeleteAsync
        // o el comando de admin): IsDeleted = 1, FechaCierre sigue null.
        await context.Set<VacanteEntity>()
            .Where(v => v.Id == primera.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDeleted, true));
        await context.SaveChangesAsync();

        // Reabrir el Puesto con otra Vacante. La columna calculada de la
        // primera evalúa a NULL porque IsDeleted = 1 — la constraint no
        // detecta conflicto.
        var segunda = new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puesto.Id,
            EstadoVacanteId = estado.Id,
            FechaApertura = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            Motivo = $"Segunda {suffix}",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        await context.Set<VacanteEntity>().AddAsync(segunda);
        await context.SaveChangesAsync(); // NO debe lanzar

        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var activas = await verifyContext.Set<VacanteEntity>()
            .Where(v => v.PuestoId == puesto.Id
                && v.FechaCierre == null
                && !v.IsDeleted)
            .CountAsync();

        Assert.Equal(1, activas);

        await LimpiarVacantesAsync(context, puesto, cargo, unidad, estado, primera, segunda);
    }

    private static async Task LimpiarVacantesAsync(
        SgvDbContext context,
        PuestoEntity puesto,
        CargoEntity cargo,
        UnidadOrganizativaEntity unidad,
        params object[] extras)
    {
        // Tras un fallo de SaveChangesAsync (constraint violation,
        // FK violation), el ChangeTracker puede tener entidades en
        // estados mixtos. Limpiamos el tracker y trabajamos con PKs
        // explícitos para evitar "association severed" en el Remove.
        context.ChangeTracker.Clear();

        // Topológico: dependientes primero, principales al final. Borrar
        // raw por PK evita que EF intente reconciliar navigation properties
        // al pasar entidades de Unchanged → Deleted.
        var orden = new List<(
            string Tabla,
            string WhereClausula,
            object[] Args)>();

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
            }
        }
        orden.Add(("Vacantes", "`PuestoId` = {0}", new object[] { puesto.Id.ToString() }));
        orden.Add(("EstadosVacante", "`Id` = {0}", new object[] { Guid.Empty.ToString() })); // no-op catch
        orden.Add(("Puestos", "`Id` = {0}", new object[] { puesto.Id.ToString() }));
        orden.Add(("Cargos", "`Id` = {0}", new object[] { cargo.Id.ToString() }));
        orden.Add(("UnidadesOrganizativas", "`Id` = {0}", new object[] { unidad.Id.ToString() }));

        foreach (var (tabla, where, args) in orden)
        {
            if (args[0] is string s && Guid.Parse(s) == Guid.Empty) continue;
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM `{tabla}` WHERE {where}",
                    args);
            }
            catch
            {
                // Best-effort cleanup: si MySQL no está o la tabla ya fue
                // vaciada, no queremos enmascarar el resultado del test.
            }
        }
    }
}
