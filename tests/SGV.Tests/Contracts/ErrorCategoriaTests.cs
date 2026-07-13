using System.Reflection;
using SGV.Contracts.Comun;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Aprobación de contrato para <see cref="ErrorCategoria"/>.
///
/// El enum es nuevo (issue #125) y constituye la taxonomía común de errores
/// para todos los <c>*CommandResult</c> y <c>*DeleteResult</c> de
/// <c>SGV.Contracts</c>. Estos tests blindan dos invariantes:
//
// <list type="number">
///   <item>El enum expone exactamente siete variantes con ordinales fijos
///         en el orden 0..6 (<c>NotFound</c>, <c>Conflict</c>,
///         <c>Validation</c>, <c>Unauthorized</c>, <c>Forbidden</c>,
///         <c>Transport</c>, <c>Unexpected</c>). El ordinal de cada
///         variante es contrato público: se preserva append-only y NO se
///         reordena ni se reasigna (ver design §2.1).</item>
///   <item><c>SGV.Contracts</c> permanece leaf: el csproj no contiene
///         <c>ProjectReference</c> a otros proyectos del grafo.
///         Esta invariante es estructural para el grafo
///         <c>Dominio ← Aplicacion ← Contracts ← {Api, Web}</c>.</item>
/// </list>
///
/// Si alguien futuro agrega una variante nueva, reordena ordinales o
/// introduce una ProjectReference, estos tests fallan y exponen la
/// regresión antes de que el cambio llegue a los call sites.
/// </summary>
public sealed class ErrorCategoriaTests
{
    [Fact]
    public void Enum_HasSevenVariantsInOrder()
    {
        var values = Enum.GetValues<ErrorCategoria>()
            .Cast<ErrorCategoria>()
            .OrderBy(c => (int)c)
            .ToArray();

        Assert.Equal(7, values.Length);
        Assert.Equal(ErrorCategoria.NotFound, values[0]);
        Assert.Equal(ErrorCategoria.Conflict, values[1]);
        Assert.Equal(ErrorCategoria.Validation, values[2]);
        Assert.Equal(ErrorCategoria.Unauthorized, values[3]);
        Assert.Equal(ErrorCategoria.Forbidden, values[4]);
        Assert.Equal(ErrorCategoria.Transport, values[5]);
        Assert.Equal(ErrorCategoria.Unexpected, values[6]);

        Assert.Equal(0, (int)ErrorCategoria.NotFound);
        Assert.Equal(1, (int)ErrorCategoria.Conflict);
        Assert.Equal(2, (int)ErrorCategoria.Validation);
        Assert.Equal(3, (int)ErrorCategoria.Unauthorized);
        Assert.Equal(4, (int)ErrorCategoria.Forbidden);
        Assert.Equal(5, (int)ErrorCategoria.Transport);
        Assert.Equal(6, (int)ErrorCategoria.Unexpected);
    }

    [Fact]
    public void ContractsProject_HasNoProjectReferences_AndStaysLeaf()
    {
        // El csproj vive en el repo; esta ruta es estable en CI y local.
        var csprojPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SGV.Contracts", "SGV.Contracts.csproj");
        csprojPath = Path.GetFullPath(csprojPath);

        Assert.True(File.Exists(csprojPath),
            $"Contracts csproj not found at '{csprojPath}'. Ajustá el path en el test.");

        var doc = new System.Xml.XmlDocument();
        doc.Load(csprojPath);

        var ns = doc.DocumentElement!.NamespaceURI;
        var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ms", ns);

        var projectRefs = doc.SelectNodes("//ms:ProjectReference", nsmgr);
        Assert.NotNull(projectRefs);
        Assert.Empty(projectRefs!);
    }
}
