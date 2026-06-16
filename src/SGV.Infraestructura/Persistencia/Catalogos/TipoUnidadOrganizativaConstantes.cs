namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the 7 seed Guids of the TipoUnidadOrganizativa catalog.
/// Referenced by:
///   1. The EF Core migration's InsertData (introduces the rows on first apply).
///   2. DatosSemilla.HasData (EF Core model snapshot path, so the row count is stable).
/// Drift between the two is asserted by the test
/// "DatosSemilla_SeedIdsMatchTipoUnidadOrganizativaConstantes".
/// </summary>
internal static class TipoUnidadOrganizativaConstantes
{
    public static readonly Guid InstitucionId   = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid FacultadId      = Guid.Parse("60000000-0000-0000-0000-000000000002");
    public static readonly Guid SecretariaId    = Guid.Parse("60000000-0000-0000-0000-000000000003");
    public static readonly Guid DireccionId     = Guid.Parse("60000000-0000-0000-0000-000000000004");
    public static readonly Guid DepartamentoId  = Guid.Parse("60000000-0000-0000-0000-000000000005");
    public static readonly Guid DivisionId      = Guid.Parse("60000000-0000-0000-0000-000000000006");
    public static readonly Guid AreaId          = Guid.Parse("60000000-0000-0000-0000-000000000007");
}
