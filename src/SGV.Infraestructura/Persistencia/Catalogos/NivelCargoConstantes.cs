namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the seed values of the NivelCargo catalog.
/// Referenced by:
///   1. The EF Core migration's InsertData and UpdateData
///      (<c>20260618180508_CambiarNivelStringANivelId</c>).
///   2. <c>DatosSemilla.HasData</c> (EF Core model snapshot path, so the row
///      count is stable).
///   3. Any application code that needs to reference a seed NivelCargo by Id,
///      Codigo, Nombre, ValorNumerico, or Orden.
///
/// The migration consumes the 4 seeds via the <see cref="Semilla"/> array,
/// and the UpdateData statements for Cargos reference the individual
/// <c>XxxId</c> constants. DatosSemilla uses the individual constants for all
/// five properties. This guarantees the migration, the model snapshot, and the
/// seed data cannot drift apart silently.
///
/// Drift is asserted by the tests in
/// <c>NivelCargoConstantesTests</c>:
///   - <c>Migration_NoContieneGuidsLiterales_ParaNivelesCargo</c>
///   - <c>Migration_ReferenciaConstantes_DirectivoIdYConduccionMediaId</c>
///   - <c>Migration_ReferenciaConstantes_OperativoIdYAcademicoId</c>
///   - <c>Migration_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden</c>
/// </summary>
internal static class NivelCargoConstantes
{
    // ===== Ids =====
    public static readonly Guid DirectivoId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ConduccionMediaId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    public static readonly Guid OperativoId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    public static readonly Guid AcademicoId = Guid.Parse("70000000-0000-0000-0000-000000000004");

    // ===== Codigo =====
    public const string DirectivoCodigo = "Directivo";
    public const string ConduccionMediaCodigo = "ConduccionMedia";
    public const string OperativoCodigo = "Operativo";
    public const string AcademicoCodigo = "Academico";

    // ===== Nombre =====
    public const string DirectivoNombre = "Directivo";
    public const string ConduccionMediaNombre = "Conducción Media";
    public const string OperativoNombre = "Operativo";
    public const string AcademicoNombre = "Académico";

    // ===== ValorNumerico =====
    public const byte DirectivoValorNumerico = 1;
    public const byte ConduccionMediaValorNumerico = 2;
    public const byte OperativoValorNumerico = 3;
    public const byte AcademicoValorNumerico = 4;

    // ===== Orden =====
    public const int DirectivoOrden = 1;
    public const int ConduccionMediaOrden = 2;
    public const int OperativoOrden = 3;
    public const int AcademicoOrden = 4;

    /// <summary>
    /// The 4 NivelCargo seeds in their canonical order. Consumed by the
    /// migration's <c>InsertData</c> to keep the InsertData block in sync
    /// with the constants without manual duplication.
    /// </summary>
    public static readonly IReadOnlyList<NivelCargoSeed> Semilla =
    [
        new NivelCargoSeed(DirectivoId, DirectivoCodigo, DirectivoNombre, DirectivoValorNumerico, DirectivoOrden),
        new NivelCargoSeed(ConduccionMediaId, ConduccionMediaCodigo, ConduccionMediaNombre, ConduccionMediaValorNumerico, ConduccionMediaOrden),
        new NivelCargoSeed(OperativoId, OperativoCodigo, OperativoNombre, OperativoValorNumerico, OperativoOrden),
        new NivelCargoSeed(AcademicoId, AcademicoCodigo, AcademicoNombre, AcademicoValorNumerico, AcademicoOrden),
    ];
}

/// <summary>
/// One row of the <see cref="NivelCargoConstantes.Semilla"/> table. Exposed as
/// a record so the migration can build its <c>object[,]</c> from the constants
/// without duplicating the (Id, Codigo, Nombre, ValorNumerico, Orden) tuple.
/// </summary>
internal sealed record NivelCargoSeed(
    Guid Id,
    string Codigo,
    string Nombre,
    byte ValorNumerico,
    int Orden);
