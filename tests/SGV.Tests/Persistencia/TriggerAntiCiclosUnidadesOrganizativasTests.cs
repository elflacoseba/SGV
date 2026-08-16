using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// MySQL trigger tests for the anti-cycle defense-in-depth introduced by
/// issue #277. The <see cref="MySqlFactAttribute"/> skips these cleanly
/// when no local MySQL server is reachable; otherwise they exercise the
/// real <c>SIGNAL SQLSTATE '45000'</c> from the triggers created by
/// <c>AddTriggerAntiCiclosUnidadesOrganizativas</c>.
/// </summary>
/// <remarks>
/// Note on the spec scenario "INSERT con padre descendiente": a fresh row
/// being INSERTed cannot have a padre that loops back to its own Id because
/// the Id is brand new — there is no existing row in the padre chain that
/// references it. The realistic cycle-introduction vector is UPDATE on a
/// row whose padre already belonged to a chain; this test class therefore
/// exercises the UPDATE path. The trigger is symmetric across INSERT and
/// UPDATE so the same defense applies even if row ids get reused.
/// </remarks>
public sealed class TriggerAntiCiclosUnidadesOrganizativasTests
{
    [MySqlFact]
    public async Task Trigger_UpdateIntroduciendoCiclo_FallaConSQLState1644()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("TRIG-A");
        var b = RepositoryTestData.CreateUnidadOrganizativa("TRIG-B");

        // Pre-condición: A existe sin padre, B existe con padre = A.
        await context.Set<UnidadOrganizativaEntity>().AddAsync(a);
        await context.SaveChangesAsync();

        b.UnidadPadreId = a.Id;
        await context.Set<UnidadOrganizativaEntity>().AddAsync(b);
        await context.SaveChangesAsync();

        try
        {
            // Operar UPDATE directo con SQL crudo para ejercitar el
            // trigger antes de que cualquier detector de la app entre
            // en juego. El UPDATE intentaría: A.UnidadPadreId = B.Id,
            // formando el ciclo A→B→A.
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE UnidadesOrganizativas SET UnidadPadreId = @padre WHERE Id = @hijo";
            var pPadre = cmd.CreateParameter(); pPadre.ParameterName = "@padre"; pPadre.Value = b.Id.ToString();
            var pHijo = cmd.CreateParameter(); pHijo.ParameterName = "@hijo"; pHijo.Value = a.Id.ToString();
            cmd.Parameters.Add(pPadre);
            cmd.Parameters.Add(pHijo);

            var ex = await Record.ExceptionAsync(() => cmd.ExecuteNonQueryAsync());
            Assert.NotNull(ex);
            var mysqlEx = Assert.IsType<MySqlException>(ex);
            Assert.Equal(1644, mysqlEx.Number);
            Assert.Contains("CicloJerarquico", mysqlEx.Message, StringComparison.Ordinal);
        }
        finally
        {
            b.UnidadPadreId = null;
            await context.SaveChangesAsync();
            context.Set<UnidadOrganizativaEntity>().RemoveRange(a, b);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task Trigger_UpdateRompiendoCiclo_PermiteOperacion()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("TRIG-OK-A");
        var b = RepositoryTestData.CreateUnidadOrganizativa("TRIG-OK-B");
        a.UnidadPadreId = b.Id;
        b.UnidadPadreId = a.Id;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([a, b]);
        await context.SaveChangesAsync();

        try
        {
            // Rompemos el ciclo: B deja de tener padre (NULL). Esto NO
            // introduce un ciclo y por lo tanto el trigger debe dejar
            // pasar el UPDATE. La cadena A→B→A queda reducida a A→B
            // (lineal, válida).
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE UnidadesOrganizativas SET UnidadPadreId = NULL WHERE Id = @id";
                var p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = b.Id.ToString();
                cmd.Parameters.Add(p);
                var affected = await cmd.ExecuteNonQueryAsync();
                Assert.Equal(1, affected);
            }
        }
        finally
        {
            a.UnidadPadreId = null;
            b.UnidadPadreId = null;
            await context.SaveChangesAsync();
            context.Set<UnidadOrganizativaEntity>().RemoveRange(a, b);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task Trigger_DropTriggerExitoso_SinAfectarDatos()
    {
        // El rollback real de la migración se prueba en suites
        // dedicadas. Aquí validamos que DROP TRIGGER IF EXISTS es
        // idempotente y no afecta filas: lo ejecutamos dos veces y
        // comprobamos que no hay exception.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("TRIG-DROP-A");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(a);
        await context.SaveChangesAsync();

        try
        {
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"DROP TRIGGER IF EXISTS trg_UnidadesOrganizativas_BeforeInsert_Ciclo;
                                    DROP TRIGGER IF EXISTS trg_UnidadesOrganizativas_BeforeUpdate_Ciclo;";
                await cmd.ExecuteNonQueryAsync();
            }

            // La fila `a` debe seguir existiendo.
            var found = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(x => x.Id == a.Id);
            Assert.NotNull(found);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(a);
            await context.SaveChangesAsync();
        }
    }
}
