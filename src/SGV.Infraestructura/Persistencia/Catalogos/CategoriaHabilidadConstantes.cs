namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the seed values of the
/// <c>CategoriaHabilidad</c> catalog (issue
/// migrar-campo-categoria-habilidades-a-tabla).
///
/// Block <c>72000000-0000-0000-0000-000000000000</c> through
/// <c>72000000-0000-0000-0000-00000000000F</c> is reserved for
/// <c>CategoriaHabilidad</c> per the project's GUID range map
/// (see <c>docs/decisiones-implementacion.md</c> § "Mapa de bloques GUID").
/// The first 4 ids in the block are the seed rows; subsequent rows in the
/// block (…004 onward) are reserved for future seed additions.
///
/// Referenced by:
///   1. The EF Core migration's <c>InsertData</c>.
///   2. <c>DatosSemilla.HasData</c> (EF Core model snapshot path).
///   3. Any application code that needs to reference a seed
///      <c>CategoriaHabilidad</c> by Id or Codigo.
///
/// Drift is asserted by:
///   - <c>CategoriaHabilidadConstantesTests</c> (infrastructure-level).
///   - <c>DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes</c>
///     (covered via <c>DatosSemillaTests</c>).
/// </summary>
internal static class CategoriaHabilidadConstantes
{
    // ===== Ids (bloque 72000000-…) =====
    public static readonly Guid ConduccionId = Guid.Parse("72000000-0000-0000-0000-000000000000");
    public static readonly Guid TecnicaId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    public static readonly Guid DominioId = Guid.Parse("72000000-0000-0000-0000-000000000002");
    public static readonly Guid AcademicaId = Guid.Parse("72000000-0000-0000-0000-000000000003");

    // ===== Codigo =====
    public const string ConduccionCodigo = "Conduccion";
    public const string TecnicaCodigo = "Tecnica";
    public const string DominioCodigo = "Dominio";
    public const string AcademicaCodigo = "Academica";

    // ===== Nombre =====
    public const string ConduccionNombre = "Conducción";
    public const string TecnicaNombre = "Técnica";
    public const string DominioNombre = "Dominio";
    public const string AcademicaNombre = "Académica";

    /// <summary>
    /// The 4 <c>CategoriaHabilidad</c> seeds in their canonical order.
    /// Consumido por la migración <c>InsertData</c> y por
    /// <c>DatosSemilla.HasData</c> para mantener la definición del seed en
    /// un único lugar.
    /// </summary>
    public static readonly IReadOnlyList<CategoriaHabilidadSeed> Semilla =
    [
        new CategoriaHabilidadSeed(ConduccionId, ConduccionCodigo, ConduccionNombre),
        new CategoriaHabilidadSeed(TecnicaId, TecnicaCodigo, TecnicaNombre),
        new CategoriaHabilidadSeed(DominioId, DominioCodigo, DominioNombre),
        new CategoriaHabilidadSeed(AcademicaId, AcademicaCodigo, AcademicaNombre),
    ];
}

/// <summary>
/// One row of the <see cref="CategoriaHabilidadConstantes.Semilla"/> table.
/// Expuesto como record para que la migración pueda armar su <c>object[,]</c>
/// desde las constantes sin duplicar el tuple (Id, Codigo, Nombre).
/// </summary>
internal sealed record CategoriaHabilidadSeed(
    Guid Id,
    string Codigo,
    string Nombre);