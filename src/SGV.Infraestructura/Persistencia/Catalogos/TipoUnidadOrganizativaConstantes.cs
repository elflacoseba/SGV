namespace SGV.Infraestructura.Persistencia.Catalogos;

/// <summary>
/// Single source of truth for the 20 seed Guids of the TipoUnidadOrganizativa catalog.
/// Referenced by:
///   1. The EF Core migration's InsertData (introduces the rows on first apply).
///   2. DatosSemilla.HasData (EF Core model snapshot path, so the row count is stable).
/// Drift between the two is asserted by the test
/// "DatosSemilla_SeedIdsMatchTipoUnidadOrganizativaConstantes".
/// </summary>
internal static class TipoUnidadOrganizativaConstantes
{
    public static readonly Guid InstitucionId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid FacultadId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    public static readonly Guid SecretariaId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    public static readonly Guid DireccionId = Guid.Parse("60000000-0000-0000-0000-000000000004");
    public static readonly Guid DepartamentoId = Guid.Parse("60000000-0000-0000-0000-000000000005");
    public static readonly Guid DivisionId = Guid.Parse("60000000-0000-0000-0000-000000000006");
    public static readonly Guid AreaId = Guid.Parse("60000000-0000-0000-0000-000000000007");
    public static readonly Guid SedeId = Guid.Parse("60000000-0000-0000-0000-000000000008");
    public static readonly Guid RegionId = Guid.Parse("60000000-0000-0000-0000-000000000009");
    public static readonly Guid GerenciaId = Guid.Parse("60000000-0000-0000-0000-00000000000a");
    public static readonly Guid VicepresidenciaId = Guid.Parse("60000000-0000-0000-0000-00000000000b");
    public static readonly Guid SubgerenciaId = Guid.Parse("60000000-0000-0000-0000-00000000000c");
    public static readonly Guid CoordinacionId = Guid.Parse("60000000-0000-0000-0000-00000000000d");
    public static readonly Guid SeccionId = Guid.Parse("60000000-0000-0000-0000-00000000000e");
    public static readonly Guid OficinaId = Guid.Parse("60000000-0000-0000-0000-00000000000f");
    public static readonly Guid EquipoId = Guid.Parse("60000000-0000-0000-0000-000000000010");
    public static readonly Guid CelulaId = Guid.Parse("60000000-0000-0000-0000-000000000011");
    public static readonly Guid PlantaId = Guid.Parse("60000000-0000-0000-0000-000000000012");
    public static readonly Guid SucursalId = Guid.Parse("60000000-0000-0000-0000-000000000013");
    public static readonly Guid EscuelaId = Guid.Parse("60000000-0000-0000-0000-000000000014");
}
