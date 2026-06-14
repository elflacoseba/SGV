namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Puesto. Includes related entity summaries
/// (UnidadOrganizativaNombre, CargoNombre) as specified by the API contract.
/// </summary>
public sealed record PuestoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid UnidadOrganizativaId,
    string UnidadOrganizativaNombre,
    Guid CargoId,
    string CargoNombre,
    Guid? PuestoSuperiorId
);
