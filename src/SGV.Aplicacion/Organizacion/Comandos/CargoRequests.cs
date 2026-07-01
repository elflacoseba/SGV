namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Request to create a new Cargo.
/// </summary>
public sealed record CrearCargoRequest(
    string Codigo,
    string Nombre,
    Guid NivelId,
    string? Descripcion = null
);

/// <summary>
/// Request to update editable fields of an existing Cargo.
/// Codigo is editable in update; uniqueness against other active Cargos
/// is enforced by the application service (pre-check) and the unique index
/// on the ActiveCodigoUnique computed column (final arbiter).
/// </summary>
public sealed record ActualizarCargoRequest(
    string Codigo,
    string Nombre,
    Guid NivelId,
    string? Descripcion = null
);
