namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Habilidad.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// la columna legacy <c>Categoria</c> (string) se reemplaza por la FK
/// <c>CategoriaId</c> (Guid?) + navegación <see cref="CategoriaHabilidadEntity"/>.
/// La columna se elimina físicamente en la migración
/// <c>AddCategoriaHabilidadCatalog</c>.
/// </summary>
public sealed class HabilidadEntity : AuditableEntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>
    /// FK opcional al catálogo <c>CategoriasHabilidad</c>.
    /// La FK constraint se crea con <c>OnDelete(Restrict)</c> en la migración
    /// <c>AddCategoriaHabilidadCatalog</c>.
    /// </summary>
    public Guid? CategoriaId { get; set; }

    /// <summary>
    /// Navegación al catálogo <c>CategoriasHabilidad</c>. La hidratación
    /// depende de que el repositorio haga <c>Include</c> o proyección LEFT
    /// JOIN explícita.
    /// </summary>
    public CategoriaHabilidadEntity? Categoria { get; set; }

    public bool IsActive { get; set; }
}