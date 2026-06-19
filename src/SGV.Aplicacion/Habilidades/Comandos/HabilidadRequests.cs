namespace SGV.Aplicacion.Habilidades.Comandos;

/// <summary>
/// Request to create a new Habilidad.
/// </summary>
public sealed record CrearHabilidadRequest(
    string Codigo,
    string Nombre,
    string? Categoria = null,
    string? Descripcion = null
);

/// <summary>
/// Request to update editable fields of an existing Habilidad.
/// Codigo is NOT included — it is immutable after creation.
/// </summary>
public sealed record ActualizarHabilidadRequest(
    string Nombre,
    string? Categoria = null,
    string? Descripcion = null
);
