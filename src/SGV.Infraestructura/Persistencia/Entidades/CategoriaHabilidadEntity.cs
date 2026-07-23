namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia del catálogo <c>CategoriaHabilidad</c> (issue
/// migrar-campo-categoria-habilidades-a-tabla). Catálogo inmutable — no
/// tiene <c>IsActive</c>/<c>IsDeleted</c> (REQ-SPA-EVOLUTION-001
/// condición #1, paridad con <see cref="TipoDocumentoEntity"/>).
/// </summary>
public sealed class CategoriaHabilidadEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
}