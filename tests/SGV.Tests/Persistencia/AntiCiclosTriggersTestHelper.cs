using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Helpers compartidos por los tests <c>[MySqlFact]</c> del issue #277
/// para sembrar y restaurar los triggers anti-ciclo. Los scripts viven aquí
/// (no en el código de aplicación) porque son artefactos de testing: los
/// tests los invocan sobre la conexión EF del contexto pero el código
/// productivo nunca los deshabilita.
///
/// IMPORTANTE: estos helpers están pensados para ejecutarse dentro de un
/// test marcado con <c>[Collection(MySqlIntegrationCollection.Name)]</c>,
/// que serializa toda la suite y deshabilita la paralelización. Si dos
/// tests corrieran en paralelo y uno deshabilitara los triggers mientras
/// el otro los necesita, el segundo vería falsos positivos/negativos.
/// </summary>
internal static class AntiCiclosTriggersTestHelper
{
    /// <summary>
    /// Nombre de los triggers que crea la migración
    /// <c>20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas</c>.
    /// </summary>
    public const string TriggerBeforeInsert = "trg_UnidadesOrganizativas_BeforeInsert_Ciclo";
    public const string TriggerBeforeUpdate = "trg_UnidadesOrganizativas_BeforeUpdate_Ciclo";

    /// <summary>
    /// SQL literal de creación de los triggers. Debe coincidir byte a byte
    /// con el <c>Up</c> de la migración; cualquier divergencia invalida la
    /// prueba. Si modificás la migración, sincronizá este método.
    /// </summary>
    private const string ScriptCrearTriggers = @"
CREATE TRIGGER trg_UnidadesOrganizativas_BeforeInsert_Ciclo
BEFORE INSERT ON UnidadesOrganizativas
FOR EACH ROW
BEGIN
  IF NEW.UnidadPadreId IS NOT NULL THEN
    SET @sgv_ciclo_count := 0;
    WITH RECURSIVE padre_chain (id, depth) AS (
      SELECT NEW.UnidadPadreId, 0
      UNION ALL
      SELECT u.UnidadPadreId, p.depth + 1
      FROM UnidadesOrganizativas u
      INNER JOIN padre_chain p ON u.Id = p.id
      WHERE u.IsDeleted = 0 AND p.depth < 32
    )
    SELECT COUNT(*) INTO @sgv_ciclo_count FROM padre_chain WHERE id = NEW.Id;
    IF @sgv_ciclo_count > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico';
    END IF;
  END IF;
END;

CREATE TRIGGER trg_UnidadesOrganizativas_BeforeUpdate_Ciclo
BEFORE UPDATE ON UnidadesOrganizativas
FOR EACH ROW
BEGIN
  IF NEW.UnidadPadreId IS NOT NULL THEN
    SET @sgv_ciclo_count := 0;
    WITH RECURSIVE padre_chain (id, depth) AS (
      SELECT NEW.UnidadPadreId, 0
      UNION ALL
      SELECT u.UnidadPadreId, p.depth + 1
      FROM UnidadesOrganizativas u
      INNER JOIN padre_chain p ON u.Id = p.id
      WHERE u.IsDeleted = 0 AND p.depth < 32
    )
    SELECT COUNT(*) INTO @sgv_ciclo_count FROM padre_chain WHERE id = NEW.Id;
    IF @sgv_ciclo_count > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico';
    END IF;
  END IF;
END";

    /// <summary>
    /// Deshabilita los triggers anti-ciclo en la base del contexto y
    /// devuelve un <see cref="IDisposable"/> que los recrea al
    /// <c>Dispose</c>. Usá esto dentro de un bloque <c>using</c> para
    /// garantizar que los triggers vuelvan a estar activos aunque el
    /// cuerpo lance excepciones.
    ///
    /// El helper dropea los triggers reales y guarda los scripts para
    /// recrearlos tal cual la migración. <b>Nunca los deja fuera de
    /// línea</b> fuera del bloque <c>using</c>.
    /// </summary>
    public static async Task<IAsyncDisposable> DisableAntiCiclosTriggersAsync(SgvDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var conn = (MySqlConnection)context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // DROP TRIGGER IF EXISTS es idempotente; si no existieran el test
        // está corriendo contra una BD sin la migración aplicada y la
        // recreación los crearía igual.
        await using (var drop = conn.CreateCommand())
        {
            drop.CommandText = "DROP TRIGGER IF EXISTS " + TriggerBeforeInsert + "; " +
                               "DROP TRIGGER IF EXISTS " + TriggerBeforeUpdate + ";";
            await drop.ExecuteNonQueryAsync();
        }

        return new Restorer(conn);
    }

    private sealed class Restorer : IAsyncDisposable
    {
        private readonly MySqlConnection _connection;
        private bool _restored;

        internal Restorer(MySqlConnection connection) => _connection = connection;

        public async ValueTask DisposeAsync()
        {
            if (_restored)
            {
                return;
            }
            _restored = true;

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = ScriptCrearTriggers;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
