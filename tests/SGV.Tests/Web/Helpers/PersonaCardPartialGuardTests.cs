using System.Text.RegularExpressions;
using Xunit;

namespace SGV.Tests.Web.Helpers;

/// <summary>
/// Guard de fuentes para el change <c>reusable-persona-card</c> (issue #219).
/// Slice 4 / PR 4 — verifica que los helpers <c>FormatDocumento</c> /
/// <c>FormatearDocumento</c> no se reintroduzcan inline en ningún
/// <c>.cshtml</c> fuera del partial unificado
/// <c>src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml</c>.
/// <para>
/// Cubre PERFMT-03: la única fuente legítima del formateo de documento
/// es <c>PersonaFormatHelper.FormatDocumento</c> (helper centralizado
/// en Slice 1). Cualquier vista que defina su propia copia inline
/// reintroduce la duplicación que el change #219 eliminó.
/// </para>
/// <para>
/// Exclusiones explícitas:
///   <list type="bullet">
///     <item><c>src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml</c> — la partial es donde el helper se invoca legítimamente.</item>
///     <item><c>src/SGV.Web/Pages/Personas/Details.cshtml</c> — fuera del scope del change #219 por design.</item>
///   </list>
/// </para>
/// </summary>
public sealed class PersonaCardPartialGuardTests
{
    /// <summary>
    /// Regex que captura cualquier definición o invocación inline de
    /// <c>FormatDocumento</c> / <c>FormatearDocumento</c> / referencia a
    /// <c>PersonaFormatHelper</c> en un archivo <c>.cshtml</c>. La
    /// guardia considera patrón sospechoso cualquier mención de la
    /// palabra (definición <c>@functions</c>, llamada
    /// <c>FormatDocumento(...)</c> o <c>FormatearDocumento(...)</c>, o
    /// referencia de helper <c>PersonaFormatHelper</c>). El partial
    /// unificado y <c>Personas/Details.cshtml</c> se excluyen del scan.
    /// </summary>
    private static readonly Regex FormatDocumentoRegex = new(
        @"\b(FormatDocumento|FormatearDocumento|PersonaFormatHelper)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string PartialPath = "src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml";
    private const string PersonasDetailsPath = "src/SGV.Web/Pages/Personas/Details.cshtml";

    [Fact]
    public void RazorSources_NoInlineFormatDocumentoDocuments_FailsWithFileList()
    {
        var repoRoot = ResolveRepoRoot();
        var pagesRoot = Path.Combine(repoRoot, "src", "SGV.Web", "Pages");

        // Safety net: la ruta del repo debe existir. Si esto falla, el
        // test infrastructure está rota y el guard no puede correr.
        Assert.True(
            Directory.Exists(pagesRoot),
            $"No se encontró src/SGV.Web/Pages a partir de {repoRoot}. " +
            "Ajustar ResolveRepoRoot si cambia la topología del repo.");

        var violations = new List<string>();
        foreach (var cshtml in Directory.EnumerateFiles(pagesRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var normalized = cshtml.Replace('\\', '/');
            // Excluir el partial unificado (única fuente legítima).
            if (normalized.EndsWith(PartialPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // Excluir Personas/Details (fuera del scope del change).
            if (normalized.EndsWith(PersonasDetailsPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(cshtml);
            var matches = FormatDocumentoRegex.Matches(content);
            if (matches.Count > 0)
            {
                var lineNumbers = string.Join(
                    ", ",
                    matches
                        .Select(match => GetLineNumber(content, match.Index))
                        .Distinct()
                        .OrderBy(n => n));
                violations.Add($"{normalized} (líneas: {lineNumbers})");
            }
        }

        Assert.True(
            violations.Count == 0,
            "PERFMT-03 violado: los siguientes .cshtml fuera de la partial " +
            "unificada definen o invocan FormatDocumento / FormatearDocumento / " +
            "PersonaFormatHelper inline. Mover al helper PersonaFormatHelper o " +
            "delegar en _PersonaCard.cshtml. Archivos: " +
            string.Join(" | ", violations));
    }

    /// <summary>
    /// Safety net explícito: confirma que las exclusiones declaradas
    /// siguen siendo válidas. Si alguien renombra el partial o mueve
    /// el Personas/Details, este test falla y obliga a actualizar
    /// tanto las constantes como el contrato documentado.
    /// </summary>
    [Fact]
    public void GuardExclusions_StillResolveToExistingFiles()
    {
        var repoRoot = ResolveRepoRoot();

        var partial = Path.Combine(repoRoot, PartialPath.Replace('/', Path.DirectorySeparatorChar));
        var personas = Path.Combine(repoRoot, PersonasDetailsPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            File.Exists(partial),
            $"La partial unificada esperada no existe: {PartialPath}. " +
            "Slice 1 debió crearla; restaurar antes de aplicar Slice 4.");
        Assert.True(
            File.Exists(personas),
            $"Personas/Details.cshtml esperado no existe: {PersonasDetailsPath}. " +
            "Si se renombró, actualizar PersonasDetailsPath en este guard.");
    }

    /// <summary>
    /// Resuelve la raíz del repo buscando <c>src/SGV.Web/Pages</c>
    /// hacia arriba desde el directorio del binario de test. Esto
    /// evita hardcodear rutas absolutas y mantiene el guard portable
    /// entre máquinas y worktrees.
    /// </summary>
    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SGV.Web", "Pages");
            if (Directory.Exists(candidate))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "No se pudo resolver la raíz del repo: src/SGV.Web/Pages no se " +
            "encontró en ningún ancestro de " + AppContext.BaseDirectory);
    }

    private static int GetLineNumber(string content, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }
        return line;
    }
}
