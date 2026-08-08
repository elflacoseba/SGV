using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests de integración MySQL para la columna <c>VacanteId</c> de
/// <c>Ocupaciones</c> introducida por el change
/// <c>vacante-ocupacion-flow-alignment</c> (T-1.6 + escenarios de
/// cumplimiento del verify-report). Validan:
/// <list type="bullet">
///   <item>FK <c>FK_Ocupaciones_Vacantes_VacanteId</c> con <c>ON DELETE RESTRICT</c>:
///         intentar borrar una Vacante con Ocupaciones derivadas debe fallar.</item>
///   <item>Round-trip con <c>VacanteId</c> persistido y recuperado correctamente.</item>
///   <item>Round-trip con <c>VacanteId = NULL</c> (backfill de Ocupaciones históricas).</item>
/// </list>
/// Cada test usa <see cref="TestSgvDbContextFactory"/> que aplica
/// <c>Database.MigrateAsync</c> automáticamente. Sin MySQL local, el
/// <see cref="MySqlFactAttribute"/> los skipea de forma limpia.
/// </summary>
public sealed class OcupacionVacanteIdPersistenciaTests
{
    [MySqlFact]
    public async Task Guardar_OcupacionConVacanteId_PersisteYRecupera()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCC-VAC-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCC-VAC-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"OCC-VAC-PER-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"OCC-VAC-PUE-{suffix}", unidad, cargo);
        var estadoAbierta = CrearEstadoVacante($"OCC-VAC-EST-{suffix}", "Abierta", esTerminal: false);
        var vacante = CrearVacante(puesto.Id, estadoAbierta.Id, $"OCC-VAC-VAC-{suffix}");

        context.AddRange(unidad, cargo, persona, puesto, estadoAbierta, vacante);
        await context.SaveChangesAsync();

        // Persistir Ocupación con VacanteId setado.
        var ocupacionEntity = new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = persona.Id,
            PuestoId = puesto.Id,
            FechaInicio = new DateOnly(2026, 7, 1),
            FechaFin = null,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = $"OCC-VAC-OBS-{suffix}",
            VacanteId = vacante.Id,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        context.Ocupaciones.Add(ocupacionEntity);
        await context.SaveChangesAsync();

        // Releer: la columna VacanteId se mantiene tras persistencia.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var fetched = await verifyContext.Ocupaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ocupacionEntity.Id);

        Assert.NotNull(fetched);
        Assert.Equal(vacante.Id, fetched!.VacanteId);
    }

    [MySqlFact]
    public async Task Guardar_OcupacionConVacanteIdNulo_PersisteYRecuperaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCC-NULL-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCC-NULL-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"OCC-NULL-PER-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"OCC-NULL-PUE-{suffix}", unidad, cargo);

        context.AddRange(unidad, cargo, persona, puesto);
        await context.SaveChangesAsync();

        // Ocupación histórica sin VacanteId (backfill pre-N2).
        var ocupacionEntity = new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = persona.Id,
            PuestoId = puesto.Id,
            FechaInicio = new DateOnly(2025, 1, 1),
            FechaFin = new DateOnly(2025, 12, 31),
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = $"OCC-NULL-OBS-{suffix}",
            VacanteId = null,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        context.Ocupaciones.Add(ocupacionEntity);
        await context.SaveChangesAsync();

        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var fetched = await verifyContext.Ocupaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ocupacionEntity.Id);

        Assert.NotNull(fetched);
        Assert.Null(fetched!.VacanteId);
    }

    [MySqlFact]
    public async Task Borrar_VacanteConOcupacionesDerivadas_BloqueaPorRestrict()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"OCC-RES-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"OCC-RES-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"OCC-RES-PER-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"OCC-RES-PUE-{suffix}", unidad, cargo);
        var estadoAbierta = CrearEstadoVacante($"OCC-RES-EST-{suffix}", "Abierta", esTerminal: false);
        var vacante = CrearVacante(puesto.Id, estadoAbierta.Id, $"OCC-RES-VAC-{suffix}");
        var ocupacionEntity = new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = persona.Id,
            PuestoId = puesto.Id,
            FechaInicio = new DateOnly(2026, 8, 1),
            FechaFin = null,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = $"OCC-RES-OBS-{suffix}",
            VacanteId = vacante.Id,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        context.AddRange(unidad, cargo, persona, puesto, estadoAbierta, vacante, ocupacionEntity);
        await context.SaveChangesAsync();

        // Verificar la regla ON DELETE RESTRICT directamente via SQL (Pomelo
        // EF puede traducir Remove+SaveChanges como SET NULL cuando la
        // columna es nullable; la fuente de verdad es la FK de la BD).
        var rows = await context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM Ocupaciones WHERE VacanteId = {0}",
                vacante.Id)
            .ToListAsync();
        Assert.Equal(1, rows[0]);

        // El DELETE directo en SQL debe lanzar MySqlException por la FK
        // ON DELETE RESTRICT. La excepción misma es la prueba de la
        // invariante: la base de datos rechaza el borrado de la Vacante.
        try
        {
            await context.Database
                .ExecuteSqlRawAsync(
                    "DELETE FROM Vacantes WHERE Id = {0}",
                    vacante.Id);
            Assert.Fail("Se esperaba MySqlException por la FK ON DELETE RESTRICT");
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Message.Contains("FK_Ocupaciones_Vacantes_VacanteId", StringComparison.OrdinalIgnoreCase))
        {
            // Esperado.
        }

        // La Vacante sigue existiendo tras el intento de DELETE.
        await using var verifyContext = new TestSgvDbContextFactory().CreateDbContext([]);
        var stillThere = await verifyContext.Vacantes
            .AsNoTracking()
            .AnyAsync(v => v.Id == vacante.Id);
        Assert.True(stillThere);
    }

    private static EstadoVacanteEntity CrearEstadoVacante(string prefix, string nombre, bool esTerminal)
    {
        return new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"{prefix}-COD",
            Nombre = nombre,
            Orden = 1,
            EsTerminal = esTerminal
        };
    }

    private static VacanteEntity CrearVacante(Guid puestoId, Guid estadoId, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puestoId,
            EstadoVacanteId = estadoId,
            Motivo = $"{prefix}-MOTIVO",
            FechaApertura = DateTime.UtcNow,
            FechaCierre = null,
            IsDeleted = false
        };
    }

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];
}
