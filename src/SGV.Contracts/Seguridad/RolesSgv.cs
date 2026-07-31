namespace SGV.Contracts.Seguridad;

/// <summary>
/// Fixed SGV role catalog for the first Identity management slice.
/// </summary>
public static class RolesSgv
{
    public const string Administrador = "Administrador";
    public const string GestorVacantes = "GestorVacantes";
    public const string Consultor = "Consultor";

    /// <summary>
    /// Roles allowed to perform vacante mutations (PB-1). Used by
    /// <c>[Authorize(Roles = RolesSgv.RolesSgvMutacion)]</c> on
    /// POST/PATCH/DELETE handlers of <c>VacantesController</c>.
    /// Comma-separated single string per ASP.NET Core convention.
    /// </summary>
    public const string RolesSgvMutacion = "Administrador,GestorVacantes";

    public static IReadOnlyList<string> Todos { get; } =
    [
        Administrador,
        GestorVacantes,
        Consultor
    ];

    public static bool EsValido(string role)
        => Todos.Contains(role, StringComparer.Ordinal);

    public static bool TodosValidos(IEnumerable<string> roles)
        => roles.All(EsValido);
}