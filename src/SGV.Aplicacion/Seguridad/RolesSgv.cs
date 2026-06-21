namespace SGV.Aplicacion.Seguridad;

/// <summary>
/// Fixed SGV role catalog for the first Identity management slice.
/// </summary>
public static class RolesSgv
{
    public const string Administrador = "Administrador";
    public const string GestorVacantes = "GestorVacantes";
    public const string Consultor = "Consultor";

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
