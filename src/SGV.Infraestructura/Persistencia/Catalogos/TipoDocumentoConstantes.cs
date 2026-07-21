namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the seed values of the TipoDocumento catalog.
///
/// Referenced by:
///   1. The EF Core migration's <c>InsertData</c> (issue #147).
///   2. <c>DatosSemilla.HasData</c> (EF Core model snapshot path).
///   3. Any application code that needs to reference a seed TipoDocumento by Id
///      or Codigo.
///
/// Block <c>71000000-0000-0000-0000-000000000000</c> is reserved for
/// <c>TipoDocumento</c> per the project's GUID range map (see proposal.md
/// § "Mapa de rangos GUID del proyecto"). The first 4 ids in the block are
/// the seed rows; subsequent rows in the block are reserved for future
/// seed additions.
///
/// Drift is asserted by:
///   - <c>TipoDocumentoConstantesTests</c> (infrastructure-level).
///   - <c>DatosSemilla_TipoDocumento_SeedIdsMatchConstantes</c>
///     (covered via <c>DatosSemillaTests</c>).
/// </summary>
internal static class TipoDocumentoConstantes
{
    // ===== Ids (bloque 71000000-…) =====
    public static readonly Guid DniId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    public static readonly Guid LeId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    public static readonly Guid LcId = Guid.Parse("71000000-0000-0000-0000-000000000003");
    public static readonly Guid PasaporteId = Guid.Parse("71000000-0000-0000-0000-000000000004");

    // ===== Codigo =====
    public const string DniCodigo = "DNI";
    public const string LeCodigo = "LE";
    public const string LcCodigo = "LC";
    public const string PasaporteCodigo = "Pasaporte";

    // ===== Nombre =====
    public const string DniNombre = "Documento Nacional de Identidad";
    public const string LeNombre = "Libreta de Enrolamiento";
    public const string LcNombre = "Libreta Cívica";
    public const string PasaporteNombre = "Pasaporte";

    // ===== PatronValidacion =====
    public const string DniPatron = @"^\d{7,8}$";
    public const string LePatron = @"^\d{6,8}$";
    public const string LcPatron = @"^\d{6,8}$";
    public const string PasaportePatron = @"^[A-Za-z]{3}\d{6}$";

    // ===== LongitudMinima / LongitudMaxima =====
    public const int DniLongitudMinima = 7;
    public const int DniLongitudMaxima = 8;
    public const int LeLongitudMinima = 6;
    public const int LeLongitudMaxima = 8;
    public const int LcLongitudMinima = 6;
    public const int LcLongitudMaxima = 8;
    public const int PasaporteLongitudMinima = 9;
    public const int PasaporteLongitudMaxima = 9;

    /// <summary>
    /// The 4 TipoDocumento seeds in their canonical order. Consumed by the
    /// migration's <c>InsertData</c> AND by <c>DatosSemilla.HasData</c> to keep
    /// the seed definition in one place.
    /// </summary>
    public static readonly IReadOnlyList<TipoDocumentoSeed> Semilla =
    [
        new TipoDocumentoSeed(DniId, DniCodigo, DniNombre, DniPatron, DniLongitudMinima, DniLongitudMaxima),
        new TipoDocumentoSeed(LeId, LeCodigo, LeNombre, LePatron, LeLongitudMinima, LeLongitudMaxima),
        new TipoDocumentoSeed(LcId, LcCodigo, LcNombre, LcPatron, LcLongitudMinima, LcLongitudMaxima),
        new TipoDocumentoSeed(PasaporteId, PasaporteCodigo, PasaporteNombre, PasaportePatron, PasaporteLongitudMinima, PasaporteLongitudMaxima),
    ];
}

/// <summary>
/// One row of the <see cref="TipoDocumentoConstantes.Semilla"/> table. Exposed
/// as a record so the migration can build its <c>object[,]</c> from the
/// constants without duplicating the (Id, Codigo, Nombre, Patron, Min, Max)
/// tuple.
/// </summary>
internal sealed record TipoDocumentoSeed(
    Guid Id,
    string Codigo,
    string Nombre,
    string PatronValidacion,
    int? LongitudMinima,
    int? LongitudMaxima);
