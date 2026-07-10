namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Request to create a new organizational unit.
/// </summary>
public sealed record CrearUnidadOrganizativaRequest(
    string Codigo,
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string? Descripcion = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    Guid? UnidadPadreId = null
);

/// <summary>
/// Request to update editable fields of an existing organizational unit.
/// <para>
/// <see cref="Codigo"/> is intentionally NOT part of this contract: the unit's
/// logical identity is immutable post-create. A <c>codigo</c> property present in
/// the incoming JSON is silently dropped by System.Text.Json default binding.
/// </para>
/// </summary>
public sealed record ActualizarUnidadOrganizativaRequest(
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string? Descripcion = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    Guid? UnidadPadreId = null
);

/// <summary>
/// Request to change the parent of an organizational unit.
/// </summary>
public sealed record CambiarUnidadPadreRequest(Guid? UnidadPadreId);
