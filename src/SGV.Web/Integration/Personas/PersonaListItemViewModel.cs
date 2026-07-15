namespace SGV.Web.Integration.Personas;

/// <summary>
/// View model de grilla para el listado web de personas activas o
/// eliminadas. Proyección Razor-side de <c>PersonaDto</c> que aplana los
/// nombres de campos al español legible para el listado y mantiene el
/// <c>Id</c> como única clave de bind para PRG/Delete/Reactivate.
/// </summary>
public sealed record PersonaListItemViewModel(
    Guid Id,
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Telefono,
    bool Activa);
