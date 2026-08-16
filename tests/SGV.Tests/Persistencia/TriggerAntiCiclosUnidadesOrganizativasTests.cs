using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Integration;
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
/// Tests <c>UpdateRompiendoCiclo</c> and <c>DropTrigger</c> disable the
/// triggers temporarily via
/// <see cref="AntiCiclosTriggersTestHelper.DisableAntiCiclosTriggersAsync"/>
/// to seed the legacy scenario they need (an existing cycle or a free
/// "no-op" trigger). All three classes that touch this schema participate
/// in <c>[Collection(MySqlIntegrationCollection.Name)]</c> so the
/// enable/disable window cannot overlap with another test.
/// 
/// Note on the spec scenario "INSERT con padre descendiente": a fresh row
/// being INSERTed cannot have a padre that loops back to its own Id because
/// the Id is brand new — there is no existing row in the padre chain that
/// references it. The realistic cycle-introduction vector is UPDATE on a
/// row whose padre already belonged to a chain; this test class therefore
/// exercises the UPDATE path. The trigger is symmetric across INSERT and
/// UPDATE so the same defense applies even if row ids get reused.
/// </remarks>
[Collection(MySqlIntegrationCollection.Name)]
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
        // Este test asume el escenario "datos legados con ciclo pre-existente
        // que se importa a esta BD antes de la migración". Para reproducirlo
        // sin resignar a la defensa anti-ciclo (que es lo que estamos
        // probando), desactivamos los triggers sólo durante la SIEMBRA y
        // dejamos que el helper los restaure. La operación bajo prueba (el
        // UPDATE que rompe el ciclo) corre con los triggers ya activos.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("TRIG-OK-A");
        var b = RepositoryTestData.CreateUnidadOrganizativa("TRIG-OK-B");

        await using (await AntiCiclosTriggersTestHelper.DisableAntiCiclosTriggersAsync(context))
        {
            // Siembra del ciclo: a↔b con FKs activas (no rompe CONSTRAINT
            // CK_UnidadesOrganizativas_UnidadPadre porque A.id != b.UnidadPadreId).
            // No podemos usar AddRangeAsync con dos FKs formando un ciclo
            // porque EF rechaza el grafo en memoria. Vamos por SQL directo:
            // 1) inserto a sin padre; 2) inserto b con padre = a; 3) UPDATE a
            //    para que apunte a b — esto cierra el ciclo a↔b.
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await InsertUnidad(conn, a);
            await InsertUnidad(conn, b, unidadPadreId: a.Id);

            await using (var close = conn.CreateCommand())
            {
                close.CommandText = "UPDATE UnidadesOrganizativas SET UnidadPadreId = @padre WHERE Id = @hijo";
                var pp = close.CreateParameter(); pp.ParameterName = "@padre"; pp.Value = b.Id.ToString();
                var ph = close.CreateParameter(); ph.ParameterName = "@hijo"; ph.Value = a.Id.ToString();
                close.Parameters.Add(pp);
                close.Parameters.Add(ph);
                await close.ExecuteNonQueryAsync();
            }
        }

        try
        {
            // El ciclo ya está sembrado y los triggers YA ESTÁN ACTIVOS
            // otra vez. Rompemos el ciclo: B deja de tener padre (NULL).
            // El trigger UPDATE permite esto porque NEW.UnidadPadreId
            // IS NULL salta el IF. La cadena A→B→A queda reducida a A→B
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
            // Limpieza: antes de borrar, anulamos el padre para que EF no
            // viole la FK self-ref. b ya quedó en NULL dentro del try, pero
            // a→b podría sobrevivir si el test falló tempranamente; lo
            // normalizamos acá.
            await using var reset = (MySqlConnection)context.Database.GetDbConnection();
            if (reset.State != System.Data.ConnectionState.Open)
            {
                await reset.OpenAsync();
            }

            await using (var clearA = reset.CreateCommand())
            {
                clearA.CommandText = "UPDATE UnidadesOrganizativas SET UnidadPadreId = NULL WHERE Id = @id";
                var p = clearA.CreateParameter();
                p.ParameterName = "@id";
                p.Value = a.Id.ToString();
                clearA.Parameters.Add(p);
                await clearA.ExecuteNonQueryAsync();
            }

            context.Set<UnidadOrganizativaEntity>().RemoveRange(a, b);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task Trigger_DropTriggerExitoso_SinAfectarDatos()
    {
        // El rollback real de la migración se prueba en suites
        // dedicadas. Aquí validamos que DROP TRIGGER IF EXISTS es
        // idempotente sobre un trigger cualquiera y no afecta filas:
        // creamos un trigger temporal con un nombre único (para no
        // colisionar con el de la migración), lo dropeamos dos veces y
        // verificamos que la fila insertada sigue existiendo.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("TRIG-DROP-A");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(a);
        await context.SaveChangesAsync();

        const string trgName = "trg_UnidadesOrganizativas_TestIdempotentDrop";

        try
        {
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // El trigger no-op solo verifica que el CREATE sobreviva un
            // doble DROP. Definido sobre AFTER INSERT no molesta a los
            // INSERTs posteriores.
            await using (var create = conn.CreateCommand())
            {
                create.CommandText = $"CREATE TRIGGER {trgName} AFTER INSERT ON UnidadesOrganizativas FOR EACH ROW SET @noop = 1";
                await create.ExecuteNonQueryAsync();
            }

            await using (var drop = conn.CreateCommand())
            {
                drop.CommandText = $"DROP TRIGGER IF EXISTS {trgName}; DROP TRIGGER IF EXISTS {trgName};";
                await drop.ExecuteNonQueryAsync();
            }

            // La fila `a` debe seguir existiendo.
            var found = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(x => x.Id == a.Id);
            Assert.NotNull(found);
        }
        finally
        {
            // Cleanup robusto: dropeamos el trigger si por algún motivo
            // quedó (segundo DROP es idempotente) y borramos la fila.
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using (var drop = conn.CreateCommand())
            {
                drop.CommandText = $"DROP TRIGGER IF EXISTS {trgName};";
                await drop.ExecuteNonQueryAsync();
            }

            context.Set<UnidadOrganizativaEntity>().Remove(a);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// INSERT directo via SQL crudo para sembrar el escenario del ciclo
    /// sin depender del grafo EF. Sólo usado dentro de tests que
    /// deshabilitaron previamente los triggers anti-ciclo.
    /// </summary>
    private static async Task InsertUnidad(MySqlConnection conn, UnidadOrganizativaEntity u, Guid? unidadPadreId = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO UnidadesOrganizativas
            (Id, Codigo, Nombre, Descripcion, VigenteDesde, VigenteHasta,
             IsActive, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId,
             IsDeleted, DeletedAt, DeletedByUserId, TipoUnidadOrganizativaId,
             UnidadPadreId)
            VALUES
            (@Id, @Codigo, @Nombre, NULL, NULL, NULL,
             1, UTC_TIMESTAMP(6), NULL, NULL, NULL,
             0, NULL, NULL, @TipoUnidadId,
             @PadreId)";

        cmd.Parameters.AddWithValue("@Id", u.Id.ToString());
        cmd.Parameters.AddWithValue("@Codigo", u.Codigo);
        cmd.Parameters.AddWithValue("@Nombre", u.Nombre);
        cmd.Parameters.AddWithValue("@TipoUnidadId", u.TipoUnidadOrganizativaId.ToString());
        cmd.Parameters.AddWithValue("@PadreId", (object?)unidadPadreId?.ToString() ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}
