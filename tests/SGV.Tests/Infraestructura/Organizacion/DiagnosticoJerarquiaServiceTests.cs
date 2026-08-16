using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Infraestructura.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Infraestructura.Organizacion;

/// <summary>
/// Tests for the cycle diagnostic service introduced by issue #277. The
/// service is invoked once at startup by <c>Program.cs</c> (and on demand
/// by operators); it must report pre-existing cycles without mutating any
/// row. All scenarios live behind <see cref="MySqlFactAttribute"/> because
/// they require a real MySQL connection (the diagnostics read directly from
/// the persistence layer).
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class DiagnosticoJerarquiaServiceTests
{
    [MySqlFact]
    public async Task DiagnosticarAsync_SinCiclos_RetornaListaVacia()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var r = RepositoryTestData.CreateUnidadOrganizativa("DIAG-R");
        var x = RepositoryTestData.CreateUnidadOrganizativa("DIAG-X");
        x.UnidadPadreId = r.Id;
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([r, x]);
        await context.SaveChangesAsync();

        try
        {
            var sut = new DiagnosticoJerarquiaService(context);
            var ciclos = await sut.DiagnosticarAsync(default);
            Assert.Empty(ciclos);
        }
        finally
        {
            // Clear padre first to satisfy CK constraint before deletion.
            x.UnidadPadreId = null;
            await context.SaveChangesAsync();
            context.Set<UnidadOrganizativaEntity>().RemoveRange(r, x);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DiagnosticarAsync_ConCiclo_RetornaCadaCicloDetectado()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        // Los triggers anti-ciclo bloquean cualquier intento de crear un
        // ciclo a partir del estado vacío de la BD. Para reproducir el
        // escenario "datos legados importados con un ciclo pre-existente",
        // los deshabilitamos sólo durante la siembra y dejamos que el
        // helper los restaure. La verificación del diagnóstico corre
        // con los triggers ya activos.
        var a = RepositoryTestData.CreateUnidadOrganizativa("DIAG-A");
        var b = RepositoryTestData.CreateUnidadOrganizativa("DIAG-B");

        await using (await AntiCiclosTriggersTestHelper.DisableAntiCiclosTriggersAsync(context))
        {
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
            var sut = new DiagnosticoJerarquiaService(context);
            var ciclos = await sut.DiagnosticarAsync(default);

            Assert.NotEmpty(ciclos);
            // El ciclo A↔B debería reportar al menos un CicloDetectado que
            // mencione ambos nodos. La forma exacta del path puede variar
            // según el orden de iteración (A→B→A o B→A→B).
            Assert.Contains(ciclos, c => c.Nodos.Contains(a.Id) && c.Nodos.Contains(b.Id));
        }
        finally
        {
            // Mismo motivo que en el test del repositorio: cuando los
            // padres son cíclicos, EF no siempre ordena correctamente la
            // nulificación de UnidadPadreId y el DELETE. Limpiamos por
            // SQL directo para evitar fallos espurios del batch.
            var conn = (MySqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using (var clearPadre = conn.CreateCommand())
            {
                clearPadre.CommandText = "UPDATE UnidadesOrganizativas SET UnidadPadreId = NULL WHERE Id IN (@a, @b)";
                var pa = clearPadre.CreateParameter(); pa.ParameterName = "@a"; pa.Value = a.Id.ToString();
                var pb = clearPadre.CreateParameter(); pb.ParameterName = "@b"; pb.Value = b.Id.ToString();
                clearPadre.Parameters.Add(pa);
                clearPadre.Parameters.Add(pb);
                await clearPadre.ExecuteNonQueryAsync();
            }

            await using (var del = conn.CreateCommand())
            {
                del.CommandText = "DELETE FROM UnidadesOrganizativas WHERE Id IN (@a, @b)";
                var pa = del.CreateParameter(); pa.ParameterName = "@a"; pa.Value = a.Id.ToString();
                var pb = del.CreateParameter(); pb.ParameterName = "@b"; pb.Value = b.Id.ToString();
                del.Parameters.Add(pa);
                del.Parameters.Add(pb);
                await del.ExecuteNonQueryAsync();
            }
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
