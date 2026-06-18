namespace SGV.Dominio.Organizacion;

/// <summary>
/// Constantes Guid estáticas para los niveles de cargo semilla.
/// Single source of truth for the 4 seed Guids used by migrations, seed data, and tests.
/// Referenced by:
///   1. NivelCargoConstantes in Infrastructure (migration InsertData).
///   2. DatosSemilla.HasData (EF Core model snapshot path).
/// </summary>
public static class NivelesCargo
{
    public static readonly Guid DirectivoId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ConduccionMediaId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    public static readonly Guid OperativoId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    public static readonly Guid AcademicoId = Guid.Parse("70000000-0000-0000-0000-000000000004");
}
