namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for TipoUnidadOrganizativa catalog. Exposes only Id, Codigo, and Nombre.
/// </summary>
public sealed record TipoUnidadOrganizativaDto(
    Guid Id,
    string Codigo,
    string Nombre
);
