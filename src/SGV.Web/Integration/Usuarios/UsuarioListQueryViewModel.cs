namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// View model bindable para <c>Index.cshtml</c> (PR 3). Mantiene los
/// parámetros del query string que la Razor Page recoge y reenvía al
/// cliente tipado; <see cref="Status"/> se persiste como string crudo
/// (<c>activas|eliminadas</c>) para no acoplar el binder Razor a la
/// enumeración del contrato. La PageModel convierte a
/// <c>UsuarioListQuery</c> (Contracts, con enum) antes de invocar al
/// cliente.
/// </summary>
/// <param name="Status">Segmento del listado (<c>activas</c> o <c>eliminadas</c>).</param>
/// <param name="Search">Término de búsqueda substring (userName|email|nombres|apellidos).</param>
/// <param name="Sort">Expresión de orden server-side (e.g. <c>userName_asc</c>).</param>
/// <param name="Page">Página actual 1-based.</param>
public sealed record UsuarioListQueryViewModel(
    string? Status,
    string? Search,
    string? Sort,
    int Page);
