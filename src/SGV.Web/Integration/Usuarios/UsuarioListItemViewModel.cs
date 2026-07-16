namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// View model de grilla para el listado web de usuarios activos o
/// eliminados. Proyección Razor-side de <c>UsuarioDto</c> que aplana los
/// nombres de campos al español legible para el listado y mantiene el
/// <c>Id</c> como única clave de bind para PRG/Delete/Reactivate.
/// </summary>
/// <remarks>
/// <para>
/// El <see cref="PersonaId"/> se proyecta desde
/// <c>UsuarioDto.PersonaId</c> para que el back-link a la Persona
/// pueda renderizarse en el listado (PR 3a — fuera de scope de PR 2).
/// </para>
/// <para>
/// <see cref="Roles"/> se persiste como <see cref="IReadOnlyList{T}"/>
/// inmutable. La página lo une con
/// <see cref="System.String.Join(string, System.Collections.Generic.IEnumerable{string})"/>
/// para mostrar la lista en la columna correspondiente.
/// </para>
/// </remarks>
public sealed record UsuarioListItemViewModel(
    string Id,
    string UserName,
    string Email,
    string? Nombres,
    string? Apellidos,
    IReadOnlyList<string> Roles,
    Guid PersonaId);
