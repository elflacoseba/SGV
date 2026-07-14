using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Docs;

/// <summary>
/// Coherencia prosa↔modelo para <c>docs/decisiones-implementacion.md</c>.
/// El modelo EF Core vigente sólo garantiza unicidad activa por Puesto
/// (<c>ActivePuestoIdUnique</c>) y por la combinación Persona + Puesto
/// (<c>ActivePersonaPuestoUnique</c>); este test blinda la sección
/// "Ocupaciones Activas" contra drift respecto del modelo y de la
/// spec canónica <c>openspec/specs/sgv-database/spec.md</c>.
/// </summary>
public sealed class CoherenciaDecisionesImplementacionTests
{
    private const string MarkdownRelativo = "docs/decisiones-implementacion.md";
    private const string SeccionOcupaciones = "Ocupaciones Activas";
    private const string ShadowPuesto = "ActivePuestoIdUnique";
    private const string ShadowPersonaPuesto = "ActivePersonaPuestoUnique";
    private const string ShadowPersonaSimple = "ActivePersonaIdUnique";

    private static readonly Lazy<string> _rutaMarkdown = new(ResolverRutaMarkdown);

    private readonly SgvDbContext _contexto = new TestSgvDbContextFactory().CreateDbContext([]);

    [Fact]
    public void Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes()
    {
        var texto = CargarMarkdown();
        var seccion = ExtraerSeccion(texto, SeccionOcupaciones);

        Assert.NotNull(seccion);

        Assert.Contains(
            ShadowPuesto,
            seccion,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            ShadowPersonaPuesto,
            seccion,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes()
    {
        var texto = CargarMarkdown();
        var seccion = ExtraerSeccion(texto, SeccionOcupaciones);

        Assert.NotNull(seccion);
        Assert.DoesNotContain(
            "Si el negocio requiere cargos concurrentes",
            seccion,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(OcupacionEntity));
        Assert.NotNull(entidad);

        AssertShadowPropertyUnicidad(entidad!, ShadowPuesto);
        AssertShadowPropertyUnicidad(entidad!, ShadowPersonaPuesto);

        var sombraPersonaSimple = entidad!.FindProperty(ShadowPersonaSimple);
        Assert.Null(sombraPersonaSimple);
    }

    private static void AssertShadowPropertyUnicidad(
        IEntityType entidad,
        string shadowProperty)
    {
        var propiedad = entidad.FindProperty(shadowProperty);
        Assert.NotNull(propiedad);

        var indiceUnico = entidad.GetIndexes()
            .Where(i => i.Properties.Any(p => p.Name == shadowProperty))
            .SingleOrDefault(i => i.IsUnique);

        Assert.NotNull(indiceUnico);
    }

    private static string ResolverRutaMarkdown()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, MarkdownRelativo);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException(
            $"No se encontró '{MarkdownRelativo}' ascendiendo desde '{AppContext.BaseDirectory}'. " +
            "Asegurate de ejecutar el test desde un cwd dentro del repo SGV.");
    }

    private static string CargarMarkdown() => File.ReadAllText(_rutaMarkdown.Value);

    private static string? ExtraerSeccion(string markdown, string encabezado)
    {
        var patron = new Regex(
            @"^##\s+" + Regex.Escape(encabezado) + @"\s*$(?<cuerpo>.*?)(?=^##\s+|\z)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var match = patron.Match(markdown);
        return match.Success ? match.Groups["cuerpo"].Value : null;
    }
}