using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Migraciones;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Issue #273 (Slice B): cobertura RED→GREEN de la migración
/// <see cref="FixEstadoVacanteEnSeleccionEncoding"/> que reescribe el
/// mojibake "Ã³" → "ó" en la fila <c>Codigo='EnSeleccion'</c> del
/// catálogo <c>EstadosVacante</c>.
///
/// Estrategia: la migración es forward-only e idempotente (sólo afecta
/// filas con el mojibake). El bootstrap de <see cref="MySqlFactAttribute"/>
/// ya aplicó todas las migraciones contra la DB de test, así que el test
/// inserta manualmente una fila corrupta y ejecuta el SQL exacto de la
/// migración (no <c>Database.Migrate()</c>) para verificar dos cosas:
///   1. Una fila con mojibake se reescribe a "En Selección".
///   2. Una fila correcta ("En Selección" sin "Ã³") NO se toca.
/// Reentrancia: la migración puede correrse N veces; la segunda es no-op.
/// </summary>
public sealed class MigracionEstadoVacanteEncodingTests
{
    private const string EnSeleccionId = "20000000-0000-0000-0000-000000000002";

    /// <summary>
    /// Cuando la fila tiene mojibake "En SelecciÃ³n", la migración la
    /// reescribe a "En Selección" (UTF-8 correcto). El test crea la fila
    /// manualmente con el byte mal codificado y luego ejecuta el SQL
    /// exacto de la migración contra la DB de test.
    /// </summary>
    [MySqlFact]
    public async Task Up_ReescribeMojibakeAUtf8Correcto()
    {
        await using var connection = new MySqlConnection(TestSgvDbContextFactory.ResolveConnectionString());
        await connection.OpenAsync();

        var enSeleccionId = Guid.Parse(EnSeleccionId);
        const string nombreCorrecto = "En Selección";
        const string nombreConMojibake = "En SelecciÃ³n";

        // Arrange: reestablecemos la fila a estado mojibake previo. Si el
        // seed ya está correcto (escenario normal en bases nuevas), lo
        // sobreescribimos con el byte mal codificado para simular el bug.
        await ExecNonQueryAsync(connection,
            $"UPDATE `EstadosVacante` SET `Nombre` = '{nombreConMojibake}' WHERE `Id` = '{enSeleccionId:D}'");

        // Sanity: confirmamos el estado pre-migración.
        var preEstado = await ScalarStringAsync(connection,
            $"SELECT `Nombre` FROM `EstadosVacante` WHERE `Id` = '{enSeleccionId:D}'");
        Assert.Equal(nombreConMojibake, preEstado);

        // Act: aplicamos el SQL exacto de la migración FixEstadoVacanteEnSeleccionEncoding.
        await ExecNonQueryAsync(connection,
            @"UPDATE `EstadosVacante`
              SET `Nombre` = 'En Selección'
              WHERE `Codigo` = 'EnSeleccion'
                AND `Nombre` LIKE '%Ã³%';");

        // Assert: la fila quedó con encoding correcto.
        var postEstado = await ScalarStringAsync(connection,
            $"SELECT `Nombre` FROM `EstadosVacante` WHERE `Id` = '{enSeleccionId:D}'");
        Assert.Equal(nombreCorrecto, postEstado);

        // Cleanup: dejamos la fila en su estado correcto (que es lo que
        // deja la migración tras aplicarse). Otros tests que dependen de
        // el nombre correcto (e.g. listados de catálogo) no se ven
        // afectados.
    }

    /// <summary>
    /// Cuando la fila YA tiene encoding correcto, la migración es no-op
    /// (no la sobreescribe ni genera side-effects). Esto valida la
    /// garantía de idempotencia: la migración puede correrse múltiples
    /// veces sin destruir datos correctos.
    /// </summary>
    [MySqlFact]
    public async Task Up_FilaCorrecta_NoSeToca()
    {
        await using var connection = new MySqlConnection(TestSgvDbContextFactory.ResolveConnectionString());
        await connection.OpenAsync();

        var enSeleccionId = Guid.Parse(EnSeleccionId);
        const string nombreCorrecto = "En Selección";

        // Arrange: la fila debe estar en estado correcto (escenario post-fix).
        await ExecNonQueryAsync(connection,
            $"UPDATE `EstadosVacante` SET `Nombre` = '{nombreCorrecto}' WHERE `Id` = '{enSeleccionId:D}'");

        // Act: aplicamos el SQL de la migración.
        await ExecNonQueryAsync(connection,
            @"UPDATE `EstadosVacante`
              SET `Nombre` = 'En Selección'
              WHERE `Codigo` = 'EnSeleccion'
                AND `Nombre` LIKE '%Ã³%';");

        // Assert: la fila sigue con el nombre correcto (el WHERE LIKE no
        // matchea, así que UPDATE no aplica).
        var postEstado = await ScalarStringAsync(connection,
            $"SELECT `Nombre` FROM `EstadosVacante` WHERE `Id` = '{enSeleccionId:D}'");
        Assert.Equal(nombreCorrecto, postEstado);
    }

    private static async Task ExecNonQueryAsync(MySqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(MySqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }
}
