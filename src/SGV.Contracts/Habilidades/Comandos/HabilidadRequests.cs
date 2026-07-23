namespace SGV.Contracts.Habilidades.Comandos;

/// <summary>
/// Request to create a new Habilidad.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// the legacy <c>string? Categoria</c> field is replaced by
/// <c>Guid? CategoriaId</c> (FK al catálogo <c>CategoriasHabilidad</c>).
/// La validación contra catálogo es responsabilidad del servicio de
/// aplicación; si <c>CategoriaId</c> no existe en el catálogo seed, la
/// respuesta es <c>400 Bad Request</c> con <c>CategoriaHabilidadNoExiste</c>.
/// </summary>
public sealed record CrearHabilidadRequest(
    string Codigo,
    string Nombre,
    Guid? CategoriaId = null,
    string? Descripcion = null
);

/// <summary>
/// Request to update editable fields of an existing Habilidad, including
/// <c>Codigo</c>. The application service re-applies the active-uniqueness
/// rule before persisting.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// el campo legacy <c>string? Categoria</c> se reemplaza por
/// <c>Guid? CategoriaId</c> (misma semántica de breaking que en create).
/// </summary>
public sealed record ActualizarHabilidadRequest(
    string Codigo,
    string Nombre,
    Guid? CategoriaId = null,
    string? Descripcion = null
);