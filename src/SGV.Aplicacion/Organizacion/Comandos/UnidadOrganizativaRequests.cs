namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Request to create a new organizational unit.
/// </summary>
public sealed record CrearUnidadOrganizativaRequest(
    string Codigo,
    string Nombre,
    string TipoUnidad,
    string? Descripcion = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    Guid? UnidadPadreId = null
);

/// <summary>
/// Request to update editable fields of an existing organizational unit.
/// </summary>
public sealed record ActualizarUnidadOrganizativaRequest(
    string Codigo,
    string Nombre,
    string TipoUnidad,
    string? Descripcion = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null
);

/// <summary>
/// Request to change the parent of an organizational unit.
/// </summary>
public sealed record CambiarUnidadPadreRequest(Guid? UnidadPadreId);
