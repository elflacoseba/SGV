using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests estáticos de defensa-en-profundidad para el script standalone
/// <c>docs/migracion-inicial-sgv.sql</c> (issue #263).
///
/// Estos tests detectan los dos patrones de bug originales sin
/// necesidad de MySQL real, usando el archivo de script como input.
/// Son complementarios al smoke test (<see cref="ScriptStandaloneSmokeMySqlFactTests"/>)
/// que sí ejecuta el script contra una base efímera.
///
/// Bug 1 (origen #263): UPDATE sin ';' dentro de un procedure
/// MigrationsScript produce ERROR 1064 dentro del wrapper --idempotent.
/// Bug 2 (origen #263): CREATE/DROP PROCEDURE anidado dentro de
/// MigrationsScript produce ERROR 1357 ("Can't drop or alter a
/// PROCEDURE from within another stored routine").
///
/// Si alguien reintroduce cualquiera de los dos patrones, este test
/// falla con un mensaje que apunta al offset aproximado del archivo.
/// </summary>
public sealed class ScriptStandaloneStaticGuardTests
{
    private const string ScriptRelativePath = "../../../../../docs/migracion-inicial-sgv.sql";

    [Fact]
    public void Script_NoContieneUpdateSinPuntoYComaDentroDeMigrationsScript()
    {
        // Patrón Bug 1 (#263): dentro de un procedure MigrationsScript,
        // una sentencia que arranca con UPDATE/INSERT/DELETE/ALTER y
        // NO termina con ';' en ninguna línea (incluyendo posibles
        // continuaciones multi-línea WHERE/VALUES/etc) produce ERROR
        // 1064. La forma específica del bug original era:
        //
        //     UPDATE `Ocupaciones` SET ... = '0' WHERE ... = 'Permanente'
        //
        //     END IF;
        //
        // (UPDATE sin ';' seguido de línea vacía y END IF). EF genera
        // statements multi-línea válidos para UpdateData (UPDATE en una
        // línea, WHERE con ';' en la siguiente), así que la heurística
        // distingue ambos: si la línea siguiente al inicio de la
        // sentencia es una continuación SQL típica (WHERE/VALUES/AND/
        // OR/SET/ON/DEFAULT/CHARACTER/COLLATION/CONSTRAINT/...) y la
        // sentencia cierra con ';' en algún punto, es válida; si la
        // sentencia no cierra con ';' antes del procedure END, es bug.
        var script = LoadScript();

        var violations = new List<string>();
        var procedureBodies = ExtractProcedureBodies(script);

        var statementStarts = new[] { "UPDATE ", "INSERT ", "DELETE ", "ALTER " };

        foreach (var (header, body) in procedureBodies)
        {
            // Quitar comentarios inline para no contar ';' dentro de '--'.
            var cleaned = System.Text.RegularExpressions.Regex.Replace(
                body, @"--[^\n]*", string.Empty);
            var lines = cleaned.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                var startsStatement = false;
                foreach (var keyword in statementStarts)
                {
                    if (trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        startsStatement = true;
                        break;
                    }
                }
                if (!startsStatement) continue;

                // Buscar el próximo ';' desde esta línea hacia adelante.
                // Una sentencia válida cierra antes del END del procedure
                // o antes de una nueva sentencia.
                //
                // IMPORTANTE: NO aceptar ';' que aparezca en líneas que
                // son palabras clave de control (END IF, END //) porque
                // ese ';' pertenece al cierre del IF o del procedure, no
                // a la sentencia actual.
                var foundSemicolon = trimmed.EndsWith(";", StringComparison.Ordinal);
                var sawClosure = false;
                var j = i + 1;
                for (; j < lines.Length; j++)
                {
                    var nextTrimmed = lines[j].TrimStart();
                    var isControlKeyword = nextTrimmed.StartsWith("END IF", StringComparison.Ordinal)
                        || nextTrimmed.StartsWith("END //", StringComparison.Ordinal);
                    if (!isControlKeyword && nextTrimmed.EndsWith(";", StringComparison.Ordinal))
                    {
                        foundSemicolon = true;
                        break;
                    }
                    if (isControlKeyword)
                    {
                        sawClosure = true;
                        break;
                    }
                    foreach (var keyword in statementStarts)
                    {
                        if (nextTrimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                            || nextTrimmed.StartsWith("SELECT ROW_COUNT", StringComparison.OrdinalIgnoreCase))
                        {
                            sawClosure = true;
                            j -= 1;
                            goto Done;
                        }
                    }
                }
            Done:
                if (j >= lines.Length)
                {
                    // Llegamos al final del procedure sin encontrar ';' — bug.
                    violations.Add(
                        $"Procedimiento '{header}' línea {i + 1}: "
                      + $"sentencia sin ';' antes del cierre del procedure.\n"
                      + $"   {trimmed}");
                }
                else if (!foundSemicolon && sawClosure)
                {
                    // Encontramos una nueva sentencia, END IF o END // sin ';' previo.
                    violations.Add(
                        $"Procedimiento '{header}' línea {i + 1}: "
                      + $"sentencia sin ';' antes del cierre.\n"
                      + $"   {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Script contiene sentencias sin ';' dentro de MigrationsScript "
          + "(#263 bug 1 reintroducido):\n" + string.Join("\n\n", violations));
    }

    [Fact]
    public void Script_NoContieneCreateNiDropProcedureAnidadosEnMigrationsScript()
    {
        // Patrón: dentro de un procedure MigrationsScript, una línea
        // `CREATE PROCEDURE <otro>` o `DROP PROCEDURE <otro>` produce
        // ERROR 1357 ("Can't drop or alter a PROCEDURE from within
        // another stored routine").
        var script = LoadScript();

        var violations = new List<string>();
        var procedureBodies = ExtractProcedureBodies(script);

        foreach (var (header, body) in procedureBodies)
        {
            var lines = body.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("CREATE PROCEDURE ", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("CREATE PROCEDURE MigrationsScript", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        $"Procedimiento '{header}' línea {i + 1}: CREATE PROCEDURE anidado.\n"
                      + $"   {trimmed}");
                }
                if (trimmed.StartsWith("DROP PROCEDURE ", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("DROP PROCEDURE MigrationsScript", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        $"Procedimiento '{header}' línea {i + 1}: DROP PROCEDURE anidado.\n"
                      + $"   {trimmed}");
                }
                if (trimmed.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        $"Procedimiento '{header}' línea {i + 1}: CALL anidado.\n"
                      + $"   {trimmed}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Script contiene CREATE/DROP PROCEDURE o CALL anidados en MigrationsScript "
          + "(#263 bug 2 reintroducido):\n" + string.Join("\n\n", violations));
    }

    [Fact]
    public void Script_D7Migration_UsaPreflightUniqueIndexEnLugarDeSignalCustom()
    {
        // (#263) La migración D7 ya no usa SIGNAL SQLSTATE custom;
        // el preflight fail-loud se modela como ADD UNIQUE INDEX
        // temporal. Si alguien revierte al SIGNAL, este test falla.
        var script = LoadScript();
        Assert.Contains(
            "__sgvD7_PreflightUnique",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SIGNAL SQLSTATE '45000'",
            ExtractD7Body(script),
            StringComparison.Ordinal);
    }

    private static string LoadScript()
    {
        var cwd = Directory.GetCurrentDirectory();
        var path = Path.GetFullPath(Path.Combine(cwd, ScriptRelativePath));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Script standalone no encontrado en '{path}'. Regeneralo.",
                path);
        }
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Devuelve una lista de (header, body) para cada procedure
    /// MigrationsScript en el script. El body es el contenido entre
    /// <c>BEGIN</c> y <c>END //</c> (inclusive del último END).
    /// </summary>
    private static IEnumerable<(string Header, string Body)> ExtractProcedureBodies(string script)
    {
        const string BeginMarker = "CREATE PROCEDURE MigrationsScript()";
        const string EndMarker = "END //";

        var idx = 0;
        while (idx < script.Length)
        {
            var start = script.IndexOf(BeginMarker, idx, StringComparison.Ordinal);
            if (start < 0) yield break;
            var begin = script.IndexOf("BEGIN", start, StringComparison.Ordinal);
            if (begin < 0) yield break;
            var end = script.IndexOf(EndMarker, begin, StringComparison.Ordinal);
            if (end < 0) yield break;

            var body = script.Substring(begin, end + EndMarker.Length - begin);
            var headerLine = "MigrationsScript@" + start;
            yield return (headerLine, body);
            idx = end + EndMarker.Length;
        }
    }

    /// <summary>
    /// Extrae el cuerpo de la migración D7 (entre CREATE PROCEDURE
    /// MigrationsScript() con su MigrationId D7 y el END //) para
    /// validación focal.
    /// </summary>
    private static string ExtractD7Body(string script)
    {
        const string MigrationIdMarker = "'20260716120000_DropSoftDeleteFromAspNetUsers'";
        var start = script.IndexOf(MigrationIdMarker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        var end = script.IndexOf("END //", start, StringComparison.Ordinal);
        if (end < 0) return string.Empty;
        return script.Substring(start, end + "END //".Length - start);
    }
}