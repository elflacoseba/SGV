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
/// Request to update editable fields of an existing Habilidad, including
/// <c>Codigo</c>. The application service re-applies the active-uniqueness
/// rule before persisting.
/// </summary>
public sealed record ActualizarHabilidadRequest(
    string Codigo,
    string Nombre,
    string? Categoria = null,
    string? Descripcion = null
);
