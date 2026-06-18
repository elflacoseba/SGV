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
/// Codigo is NOT included — it is immutable after creation.
/// </summary>
public sealed record ActualizarCargoRequest(
    string Nombre,
    Guid NivelId,
    string? Descripcion = null
);
