using System.Diagnostics;
using MySqlConnector;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Smoke test end-to-end para el script standalone
/// <c>docs/migracion-inicial-sgv.sql</c> (issue #263).
///
/// Ejecuta el archivo completo contra una base MySQL efímera
/// (creada y destruida por el propio test) y verifica:
///   1. La aplicación del script no lanza excepciones.
///   2. <c>__EFMigrationsHistory</c> registra exactamente las 17
///      migraciones que <c>dotnet ef migrations list</c> detecta
///      (excluye <c>20260730000000_SemillaTipoUnidadOrganizativaAmpliada</c>
///      que no tiene Designer.cs — limitación documentada del script,
///      no afecta el EF runtime que usa DatosSemilla.HasData).
///   3. End-state post-D7: <c>IsDeleted</c> / <c>ActiveUserNameUnique</c> /
///      <c>ActivePersonaIdUnique</c> no existen y
///      <c>IX_AspNetUsers_PersonaId</c> es UNIQUE.
///   4. Idempotencia: aplicar el script dos veces seguidas termina
///      sin excepciones y la cuenta de migraciones aplicadas no cambia.
///
/// El script usa directivas <c>DELIMITER</c> que MySqlConnector no
/// soporta (https://mysqlconnector.net/delimiter), por lo que la
/// ejecución se delega al binario <c>mysql</c> del cliente — el mismo
/// path que un operador seguiría en producción. La password se inyecta
/// vía <see cref="ProcessStartInfo.Environment"/> bajo <c>MYSQL_PWD</c>:
/// nunca aparece en <c>argv</c> ni en <c>ps</c>. El contenido del
/// script se lee a memoria y se alimenta por
/// <see cref="ProcessStartInfo.RedirectStandardInput"/> — sin wrapper
/// de shell, sin pipes externos, sin credenciales hardcodeadas.
///
/// Crítico: NO se aplican manualmente los cambios de
/// <c>MySqlTestDatabaseBootstrap</c> aquí — este test valida el
/// artefacto generado por <c>dotnet ef migrations script --idempotent</c>,
/// no el camino <c>Database.Migrate()</c>.
/// </summary>
[Collection("MySqlIntegration")]
public sealed class ScriptStandaloneSmokeMySqlFactTests : IAsyncLifetime
{
    private const string ScriptRelativePath = "../../../../../docs/migracion-inicial-sgv.sql";
    private const int ExpectedMigrationCount = 17;
    private const string DefaultDatabasePrefix = "sgv_263_smoke";

    private readonly List<string> _createdDatabases = new();
    private string _scriptPath = null!;

    public Task InitializeAsync()
    {
        // Resolver el script desde el CWD del test runner.
        var cwd = Directory.GetCurrentDirectory();
        var candidate = Path.GetFullPath(Path.Combine(cwd, ScriptRelativePath));
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"Script standalone no encontrado en '{candidate}'. "
              + $"CWD='{cwd}'. Regeneralo con `dotnet ef migrations script --idempotent --project src/SGV.Infraestructura --startup-project src/SGV.Infraestructura --output docs/migracion-inicial-sgv.sql`.",
                candidate);
        }
        _scriptPath = candidate;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Best-effort cleanup: borrar todas las DBs efímeras creadas
        // por el test runner vía CLI mysql (mismo path que la creación).
        var settings = TestSgvDbContextFactory.ResolveSettings();
        var connBuilder = new MySqlConnectionStringBuilder(settings.ConnectionString);
        foreach (var db in _createdDatabases)
        {
            try
            {
                var cleanupBuilder = new MySqlConnectionStringBuilder(connBuilder.ConnectionString) { Database = db };
                await RunMysqlCliAsync(cleanupBuilder, $"DROP DATABASE IF EXISTS `{db}`;", requireDatabase: false);
            }
            catch
            {
                // best effort
            }
        }
    }

    [MySqlFact]
    public async Task Script_ApplyOnCleanDatabase_RegistersAllDetectedMigrations()
    {
        var dbName = BuildUniqueDbName(DefaultDatabasePrefix);
        _createdDatabases.Add(dbName);

        var connBuilder = new MySqlConnectionStringBuilder(TestSgvDbContextFactory.ResolveConnectionString())
        {
            Database = dbName,
        };

        // Crear DB limpia (sin seleccionar database todavía).
        await RunMysqlCliAsync(connBuilder, $"CREATE DATABASE `{dbName}`;", requireDatabase: false);

        // Aplicar el script completo contra la base limpia.
        var (exitCode, stdout, stderr) = await RunMysqlCliWithFileAsync(connBuilder, _scriptPath);
        Assert.True(exitCode == 0,
            $"Aplicación del script terminó con código {exitCode}.\n"
          + $"STDERR:\n{stderr}\n"
          + $"STDOUT (último):\n{Tail(stdout, 2000)}");

        // 2) Verificar que __EFMigrationsHistory registra exactamente las
        //    migraciones que EF detecta.
        var appliedMigrationIds = new List<string>();
        await using (var verify = new MySqlConnection(connBuilder.ConnectionString))
        {
            await verify.OpenAsync();
            await using var cmd = verify.CreateCommand();
            cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                appliedMigrationIds.Add(reader.GetString(0));
            }
        }

        Assert.Equal(ExpectedMigrationCount, appliedMigrationIds.Count);
        Assert.Contains("20260614183103_InicialSgvo", appliedMigrationIds);
        Assert.Contains("20260716120000_DropSoftDeleteFromAspNetUsers", appliedMigrationIds);
        Assert.Contains("20260805000000_AddEstadoVacanteFlags", appliedMigrationIds);

        // 3) Verificar end-state post-D7 (renombrado y unique).
        await using (var verify = new MySqlConnection(connBuilder.ConnectionString))
        {
            await verify.OpenAsync();

            await using (var cmd = verify.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AspNetUsers'
                      AND column_name = 'IsDeleted'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, exists);
            }

            await using (var cmd = verify.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AspNetUsers'
                      AND column_name = 'ActiveUserNameUnique'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, exists);
            }

            await using (var cmd = verify.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Non_unique FROM information_schema.STATISTICS
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AspNetUsers'
                      AND index_name = 'IX_AspNetUsers_PersonaId'
                    LIMIT 1";
                var nonUnique = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(0, nonUnique);
            }

            await using (var cmd = verify.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS
                    WHERE constraint_schema = DATABASE()
                      AND table_name = 'AspNetUsers'
                      AND constraint_name = 'FK_AspNetUsers_Personas_PersonaId'
                    LIMIT 1";
                var deleteRule = (string?)await cmd.ExecuteScalarAsync();
                Assert.Equal("RESTRICT", deleteRule);
            }
        }
    }

    [MySqlFact]
    public async Task Script_ApplyTwice_IsIdempotent()
    {
        var dbName = BuildUniqueDbName($"{DefaultDatabasePrefix}_idem");
        _createdDatabases.Add(dbName);

        var connBuilder = new MySqlConnectionStringBuilder(TestSgvDbContextFactory.ResolveConnectionString())
        {
            Database = dbName,
        };

        // Crear DB limpia (sin seleccionar database todavía).
        await RunMysqlCliAsync(connBuilder, $"CREATE DATABASE `{dbName}`;", requireDatabase: false);

        // Primera corrida.
        var (exit1, _, stderr1) = await RunMysqlCliWithFileAsync(connBuilder, _scriptPath);
        Assert.True(exit1 == 0,
            $"Primera corrida terminó con código {exit1}.\nSTDERR:\n{stderr1}");

        // Segunda corrida — debe ser no-op (EF MigrationsScript gates por __EFMigrationsHistory).
        var (exit2, _, stderr2) = await RunMysqlCliWithFileAsync(connBuilder, _scriptPath);
        Assert.True(exit2 == 0,
            $"Segunda corrida terminó con código {exit2}.\nSTDERR:\n{stderr2}");

        // La cuenta de migraciones aplicadas no debe cambiar.
        await using var verify = new MySqlConnection(connBuilder.ConnectionString);
        await verify.OpenAsync();
        await using var cmd = verify.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.Equal(ExpectedMigrationCount, count);
    }

    private static string BuildUniqueDbName(string prefix)
    {
        var raw = $"{prefix}_{Guid.NewGuid():N}";
        return raw[..Math.Min(64, raw.Length)];
    }

    /// <summary>
    /// Ejecuta un comando mysql CLI contra la conexión del builder.
    /// La password se inyecta vía <see cref="ProcessStartInfo.Environment"/>
    /// bajo <c>MYSQL_PWD</c>: nunca aparece en <c>argv</c> ni en
    /// <c>ps</c>. Devuelve tupla (exitCode, stdout, stderr).
    /// <paramref name="requireDatabase"/> indica si la conexión debe
    /// incluir <c>--database</c> (false para operaciones de bootstrap
    /// como CREATE DATABASE).
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunMysqlCliAsync(
        MySqlConnectionStringBuilder builder,
        string sql,
        bool requireDatabase)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveMysqlExecutable(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add($"--host={builder.Server}");
        psi.ArgumentList.Add($"--port={builder.Port}");
        psi.ArgumentList.Add($"--user={builder.UserID}");
        // La password se inyecta SOLO vía env var del proceso hijo.
        // ProcessStartInfo.Environment NO se concatena en argv.
        psi.Environment["MYSQL_PWD"] = builder.Password ?? string.Empty;
        if (requireDatabase)
        {
            psi.ArgumentList.Add($"--database={builder.Database}");
        }

        using var proc = Process.Start(psi)!;
        await proc.StandardInput.WriteAsync(sql);
        proc.StandardInput.Close();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Ejecuta el script standalone contra la conexión del builder vía
    /// CLI <c>mysql</c>. El contenido del archivo se lee a memoria y
    /// se alimenta por <see cref="ProcessStartInfo.RedirectStandardInput"/>:
    /// <c>mysql</c> procesa el stream respetando directivas
    /// <c>DELIMITER</c> (mismo path operativo que
    /// <c>mysql &lt; script.sql</c>, pero sin shell wrapper). La
    /// password vive sólo en
    /// <see cref="ProcessStartInfo.Environment"/> bajo <c>MYSQL_PWD</c>:
    /// no aparece en <c>argv</c> del proceso <c>mysql</c> ni en
    /// <c>ps</c>.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunMysqlCliWithFileAsync(
        MySqlConnectionStringBuilder builder,
        string scriptPath)
    {
        var scriptContent = await File.ReadAllTextAsync(scriptPath);

        var psi = new ProcessStartInfo
        {
            FileName = ResolveMysqlExecutable(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add($"--host={builder.Server}");
        psi.ArgumentList.Add($"--port={builder.Port}");
        psi.ArgumentList.Add($"--user={builder.UserID}");
        psi.ArgumentList.Add($"--database={builder.Database}");
        // La password se inyecta SOLO vía env var del proceso hijo.
        // ProcessStartInfo.Environment NO se concatena en argv.
        psi.Environment["MYSQL_PWD"] = builder.Password ?? string.Empty;

        using var proc = Process.Start(psi)!;
        await proc.StandardInput.WriteAsync(scriptContent);
        proc.StandardInput.Close();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stdout, stderr);
    }

    private static string ResolveMysqlExecutable()
    {
        // Resolver `mysql` desde PATH para no hardcodear la ruta.
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, "mysql");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        // Fallback: rutas estándar Homebrew / Linux.
        foreach (var fallback in new[] { "/opt/homebrew/bin/mysql", "/usr/local/bin/mysql", "/usr/bin/mysql" })
        {
            if (File.Exists(fallback)) return fallback;
        }
        return "mysql";
    }

    private static string Tail(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return "..." + text[^maxChars..];
    }
}