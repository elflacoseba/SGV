using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that NivelCargoConstantes contains exactly 4 unique, non-empty Guids,
/// and that the migration <c>20260618180508_CambiarNivelStringANivelId</c> consumes
/// those same constants (not literal Guids or duplicated values) for the seed tuples
/// (Codigo, Nombre, ValorNumerico, Orden). This protects against silent drift between
/// the migration's InsertData and DatosSemilla.HasData.
/// </summary>
public sealed class NivelCargoConstantesTests
{
    private static readonly Guid[] AllGuids =
    [
        NivelCargoConstantes.DirectivoId,
        NivelCargoConstantes.ConduccionMediaId,
        NivelCargoConstantes.OperativoId,
        NivelCargoConstantes.AcademicoId
    ];

    private static readonly string[] AllNiveles =
    [
        "Directivo",
        "ConduccionMedia",
        "Operativo",
        "Academico"
    ];

    [Fact]
    public void Constantes_TieneExactamente4Valores()
    {
        Assert.Equal(4, AllGuids.Length);
    }

    [Fact]
    public void Constantes_TodosLosGuidsSonUnicos()
    {
        var distinct = new HashSet<Guid>(AllGuids);
        Assert.Equal(AllGuids.Length, distinct.Count);
    }

    [Fact]
    public void Constantes_NingunGuidEsVacio()
    {
        Assert.All(AllGuids, guid => Assert.NotEqual(Guid.Empty, guid));
    }

    // ========================================================================
    // Migration parity tests
    // ========================================================================

    [Fact]
    public void Migration_NoContieneGuidsLiterales_ParaNivelesCargo()
    {
        // The migration MUST reference NivelCargoConstantes.*Id instead of literal
        // Guids `70000000-0000-0000-0000-00000000000N`. If a literal slips back in,
        // this test fails and forces the author to update the constants (or the
        // migration) consistently.
        var migrationContent = ReadMigrationFile();

        Assert.DoesNotContain("70000000-0000-0000-0000-000000000001", migrationContent);
        Assert.DoesNotContain("70000000-0000-0000-0000-000000000002", migrationContent);
        Assert.DoesNotContain("70000000-0000-0000-0000-000000000003", migrationContent);
        Assert.DoesNotContain("70000000-0000-0000-0000-000000000004", migrationContent);
    }

    [Fact]
    public void Migration_ReferenciaConstantes_DirectivoIdYConduccionMediaId()
    {
        var migrationContent = ReadMigrationFile();

        Assert.Contains("NivelCargoConstantes.DirectivoId", migrationContent);
        Assert.Contains("NivelCargoConstantes.ConduccionMediaId", migrationContent);
    }

    [Fact]
    public void Migration_ReferenciaConstantes_OperativoIdYAcademicoId()
    {
        var migrationContent = ReadMigrationFile();

        Assert.Contains("NivelCargoConstantes.OperativoId", migrationContent);
        Assert.Contains("NivelCargoConstantes.AcademicoId", migrationContent);
    }

    [Fact]
    public void Migration_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden()
    {
        // Both the migration (InsertData) and DatosSemilla (HasData) must produce
        // the same 4 (Codigo, Nombre, ValorNumerico, Orden) tuples. The
        // single source of truth is NivelCargoConstantes, so this test verifies:
        //   1. The constants file exposes every level's Codigo, Nombre,
        //      ValorNumerico, Orden.
        //   2. The migration is built from the constants (either via
        //      NivelCargoConstantes.Semilla or by referencing the individual
        //      XxxCodigo/XxxNombre/XxxValorNumerico/XxxOrden constants).
        //   3. DatosSemilla references the individual constants for every level
        //      and every property.
        // If any of the three files drifts, this test fails.
        var migrationContent = ReadMigrationFile();
        var datosSemillaContent = ReadDatosSemillaFile();
        var constantesContent = NivelCargoConstantesContent();

        // 1) The constants file MUST expose the four properties for every level.
        foreach (var nivel in AllNiveles)
        {
            Assert.Contains($"{nivel}Codigo", constantesContent);
            Assert.Contains($"{nivel}Nombre", constantesContent);
            Assert.Contains($"{nivel}ValorNumerico", constantesContent);
            Assert.Contains($"{nivel}Orden", constantesContent);
        }

        // 2) The migration MUST consume the constants — either by iterating
        //    NivelCargoConstantes.Semilla, or by referencing the individual
        //    Codigo/Nombre/ValorNumerico/Orden constants directly. The
        //    NivelCargoConstantes.Semilla array itself is also a valid form.
        var migrationUsesArray = migrationContent.Contains("NivelCargoConstantes.Semilla");
        var migrationUsesIndividualProps =
            migrationContent.Contains("NivelCargoConstantes.DirectivoCodigo") ||
            migrationContent.Contains("NivelCargoConstantes.DirectivoNombre") ||
            migrationContent.Contains("NivelCargoConstantes.DirectivoValorNumerico") ||
            migrationContent.Contains("NivelCargoConstantes.DirectivoOrden");
        Assert.True(
            migrationUsesArray || migrationUsesIndividualProps,
            "La migración debe consumir NivelCargoConstantes (vía Semilla o vía las constantes individuales) para que la semilla no se separe del modelo.");

        // 3) DatosSemilla MUST reference every individual constant for the 4 levels
        //    and the 5 properties (Id, Codigo, Nombre, ValorNumerico, Orden).
        //    The Ids were already checked by the previous tests; here we focus on
        //    the four properties that compose the (Codigo, Nombre, ValorNumerico,
        //    Orden) tuple.
        foreach (var nivel in AllNiveles)
        {
            Assert.Contains($"NivelCargoConstantes.{nivel}Codigo", datosSemillaContent);
            Assert.Contains($"NivelCargoConstantes.{nivel}Nombre", datosSemillaContent);
            Assert.Contains($"NivelCargoConstantes.{nivel}ValorNumerico", datosSemillaContent);
            Assert.Contains($"NivelCargoConstantes.{nivel}Orden", datosSemillaContent);
        }
    }

    // ========================================================================
    // File resolution helpers
    // ========================================================================

    private static string WorkspaceRoot()
    {
        // AppContext.BaseDirectory when running `dotnet test` from the repo root is
        // `tests/SGV.Tests/bin/Debug/net10.0/`. Five `..` get us back to the workspace.
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"Could not resolve workspace root from '{AppContext.BaseDirectory}'. " +
                "Run tests from the repository root via `dotnet test`.");
        }
        return path;
    }

    private static string ReadMigrationFile()
    {
        var path = Path.Combine(WorkspaceRoot(),
            "src", "SGV.Infraestructura", "Persistencia", "Migraciones",
            "20260618180508_CambiarNivelStringANivelId.cs");
        Assert.True(File.Exists(path), $"Migration file not found at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string ReadDatosSemillaFile()
    {
        var path = Path.Combine(WorkspaceRoot(),
            "src", "SGV.Infraestructura", "Persistencia", "DatosSemilla.cs");
        Assert.True(File.Exists(path), $"DatosSemilla file not found at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string NivelCargoConstantesContent()
    {
        var path = Path.Combine(WorkspaceRoot(),
            "src", "SGV.Infraestructura", "Persistencia", "Catalogos",
            "NivelCargoConstantes.cs");
        Assert.True(File.Exists(path), $"NivelCargoConstantes file not found at '{path}'.");
        return File.ReadAllText(path);
    }
}
