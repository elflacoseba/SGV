using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Anti-drift cross-module: blinda explícitamente la memoria #569 y el
/// requisito Req 3 del spec <c>cargo-skill-ui-tabla-editable</c>. La
/// página <c>Habilidades.cshtml</c> es la única superficie donde podría
/// colarse un <c>Habilidad.NivelId</c> por copy-paste del patrón
/// <c>Habilidades/Create</c>; este test lee la markup como string y
/// verifica que:
/// <list type="bullet">
///   <item><description>NO exista <c>name="Habilidad.NivelId"</c> en ningún
///   <c>&lt;select&gt;</c>.</description></item>
///   <item><description>NO exista <c>&lt;select name="Habilidad.Nivel</c>
///   con ese prefijo.</description></item>
///   <item><description>SÍ exista <c>name="Actualizar[{guid}].NivelRequeridoId"</c>
///   (al menos una vez) — la grilla debe exponer el id del nivel del
///   vínculo indexado por skillId, nunca uno del catálogo maestro ni en
///   binding simple. La convención indexada es la que el
///   <c>design.md</c> sección 4 fija para anclar errores por fila.</description></item>
///   <item><description>NO exista <c>Habilidad.NivelId</c> referenciado
///   como propiedad en el PageModel (verifica el .cshtml.cs).</description></item>
/// </list>
/// Los assertions son del estilo "approval testing del contrato de
/// shape": si alguien futuro reintroduce el anti-patrón, este test
/// falla de forma ruidosa.
/// </summary>
public sealed class CargoHabilidadesAntiDriftTests
{
    private const string MarkupPath = "src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml";
    private const string PageModelPath = "src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs";

    [Fact]
    public void HabilidadesPage_NoContaminaHabilidadCatalogoConNivelRequerido()
    {
        // Resolución de path relativa al repo: las pruebas se ejecutan
        // desde el directorio del proyecto SGV.Tests, no desde la raíz.
        // Probamos dos raíces (el cwd del runner y el cwd raíz) para
        // tolerar distintas convenciones de "dotnet test".
        var markup = ReadFile(MarkupPath);
        var pageModel = ReadFile(PageModelPath);

        Assert.NotNull(markup);

        // 1) Ningún <select> debe llevar name="Habilidad.NivelId".
        Assert.DoesNotContain("name=\"Habilidad.NivelId\"", markup!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"habilidad.nivelId\"", markup!, StringComparison.OrdinalIgnoreCase);

        // 2) Ningún <select> debe llevar el prefijo "Habilidad.Nivel".
        Assert.DoesNotContain("<select name=\"Habilidad.Nivel", markup!, StringComparison.OrdinalIgnoreCase);

        // 3) La página DEBE exponer al menos un input con la convención
        // indexada Actualizar[{guid}].NivelRequeridoId en los forms de
        // Actualizar (la grilla editable por fila). Sin esto, el upsert
        // del subrecurso no podría propagar el id del nivel del vínculo
        // usando la convención que el design fija para anclar errores
        // por fila y se cae al binding simple antiguo que abandonó la
        // remediación del verify. Aceptamos tres formas equivalentes en
        // el markup fuente: literal con GUID, interpolación Razor
        // (@skill.SkillId) o variable local (@nivelKey) — lo que
        // importa es que el HTML renderizado termine con
        // name="Actualizar[<guid>].NivelRequeridoId".
        Assert.True(
            Regex.IsMatch(
                markup!,
                @"name=""Actualizar\[(?:[0-9a-fA-F\-]+|@?[A-Za-z_][A-Za-z0-9_\.]*)\]\.NivelRequeridoId""",
                RegexOptions.IgnoreCase),
            "Expected at least one form input named 'Actualizar[{guid}].NivelRequeridoId' (literal, Razor-interpolated, or Razor-local-variable) in the Habilidades.cshtml markup.");

        // 3b) Anti-regresión explícita: NO debe quedar binding simple
        // (sin prefijo Actualizar[xxx].) para los inputs de Actualizar.
        // La única excepción permitida es el input oculto "skillId" que
        // sigue viajando como campo plano en la query y en el form.
        Assert.True(
            !Regex.IsMatch(
                markup!,
                @"<(?:select|input)[^>]*name=""(?:NivelRequeridoId|Ponderacion|EsObligatoria)""[^>]*>",
                RegexOptions.IgnoreCase),
            "Detected flat (non-indexed) binding in Actualizar inputs. The remediation requires name=\"Actualizar[{guid}].Campo\".");

        // 4) El PageModel NO debe referenciar Habilidad.NivelId como
        // propiedad (memoria #569: Habilidad no tiene NivelId propio;
        // toda asociación de nivel es a través de CargoHabilidad.NivelRequeridoId).
        Assert.NotNull(pageModel);
        Assert.DoesNotContain("Habilidad.NivelId", pageModel!, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadFile(string relativePath)
    {
        // Buscar el archivo subiendo hasta 3 niveles desde el cwd del
        // proceso. Cubre los layouts típicos de `dotnet test`:
        //   - tests/SGV.Tests/bin/Debug/net10.0/ (cwd del runner)
        //   - tests/SGV.Tests/ (cwd cuando se invoca con --project)
        //   - raíz del repo
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", relativePath)
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }

        return null;
    }
}