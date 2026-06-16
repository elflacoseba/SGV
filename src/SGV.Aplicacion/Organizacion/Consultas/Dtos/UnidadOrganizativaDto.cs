namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for UnidadOrganizativa. Excludes audit and internal tracking fields.
/// </summary>
public sealed record UnidadOrganizativaDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string TipoUnidadNombre,
    string? Descripcion,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    Guid? UnidadPadreId
);
