namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the seed values of the <c>EstadoVacante</c>
/// catalog (issue feature-implementar-modulo-vacantes).
///
/// Block <c>20000000-0000-0000-0000-000000000000</c> through
/// <c>20000000-0000-0000-0000-00000000000F</c> is reserved for
/// <c>EstadoVacante</c> per the project's GUID range map (see
/// <c>docs/decisiones-implementacion.md</c> § "Mapa de bloques GUID").
/// The first 4 ids in the block are the seed rows; subsequent rows in
/// the block (…004 onward) are reserved for future seed additions.
///
/// Referenced by:
///   1. <c>DatosSemilla.HasData</c> (EF Core model snapshot path, so the
///      row count is stable).
///   2. Application code that needs to reference a seed
///      <c>EstadoVacante</c> by Id, Codigo, Nombre, Orden, or EsTerminal.
///      The command service uses <c>EsTerminal</c> to decide whether to
///      auto-set <c>FechaCierre</c> on a state transition.
///   3. Tests in <c>EstadoVacanteConstantesTests</c> that assert
///      <c>DatosSemilla_EstadoVacante_SeedIdsMatchConstantes</c>.
/// </summary>
internal static class EstadoVacanteConstantes
{
    // ===== Ids (bloque 20000000-…) =====
    public static readonly Guid AbiertaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid EnSeleccionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid CubiertaId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid CanceladaId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    // ===== Codigo =====
    public const string AbiertaCodigo = "Abierta";
    public const string EnSeleccionCodigo = "EnSeleccion";
    public const string CubiertaCodigo = "Cubierta";
    public const string CanceladaCodigo = "Cancelada";

    // ===== Nombre =====
    public const string AbiertaNombre = "Abierta";
    public const string EnSeleccionNombre = "En Selección";
    public const string CubiertaNombre = "Cubierta";
    public const string CanceladaNombre = "Cancelada";

    // ===== Orden =====
    public const int AbiertaOrden = 1;
    public const int EnSeleccionOrden = 2;
    public const int CubiertaOrden = 3;
    public const int CanceladaOrden = 4;

    // ===== EsTerminal (Cubierta + Cancelada son terminales) =====
    public const bool AbiertaEsTerminal = false;
    public const bool EnSeleccionEsTerminal = false;
    public const bool CubiertaEsTerminal = true;
    public const bool CanceladaEsTerminal = true;

    /// <summary>
    /// The 4 <c>EstadoVacante</c> seeds in their canonical order.
    /// </summary>
    public static readonly IReadOnlyList<EstadoVacanteSeed> Semilla =
    [
        new EstadoVacanteSeed(AbiertaId, AbiertaCodigo, AbiertaNombre, AbiertaOrden, AbiertaEsTerminal),
        new EstadoVacanteSeed(EnSeleccionId, EnSeleccionCodigo, EnSeleccionNombre, EnSeleccionOrden, EnSeleccionEsTerminal),
        new EstadoVacanteSeed(CubiertaId, CubiertaCodigo, CubiertaNombre, CubiertaOrden, CubiertaEsTerminal),
        new EstadoVacanteSeed(CanceladaId, CanceladaCodigo, CanceladaNombre, CanceladaOrden, CanceladaEsTerminal),
    ];
}

/// <summary>
/// One row of the <see cref="EstadoVacanteConstantes.Semilla"/> table.
/// Exposed as a record so callers can iterate the canonical seed tuples
/// without duplicating the (Id, Codigo, Nombre, Orden, EsTerminal)
/// shape.
/// </summary>
internal sealed record EstadoVacanteSeed(
    Guid Id,
    string Codigo,
    string Nombre,
    int Orden,
    bool EsTerminal);