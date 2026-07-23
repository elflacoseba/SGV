using SGV.Dominio.Comun;

namespace SGV.Dominio.Habilidades;

/// <summary>
/// Read-only catalog entity que clasifica una <see cref="Habilidad"/>. Es
/// inmutable a runtime: el catálogo se siembra exclusivamente por una
/// migración de EF Core (ver <c>CategoriaHabilidadConstantes</c>).
/// No se exponen endpoints de escritura; cualquier nueva categoría
/// requiere una nueva migración (paridad con <see cref="Personas.TipoDocumento"/>).
/// </summary>
/// <remarks>
/// No hereda de <see cref="EntidadAuditable"/> porque el catálogo es
/// inmutable y no genera auditoría de cambios (REQ-SPA-EVOLUTION-001
/// condición #1).
/// </remarks>
public sealed record class CategoriaHabilidad : EntidadBase
{
    private CategoriaHabilidad()
    {
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Sólo accesible
    /// desde <c>SGV.Infraestructura</c> y <c>SGV.Tests</c>. Las
    /// invariantes de shape (Codigo/Nombre requeridos con longitudes
    /// máximas) se replican desde el constructor primario para preservar
    /// la simetría con <see cref="Reconstitute"/> (issue #124).
    /// </summary>
    internal static CategoriaHabilidad Reconstitute(Guid id, string codigo, string nombre)
    {
        var self = new CategoriaHabilidad
        {
            Id = id
        };

        self.Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), CategoriaHabilidadRules.CodigoMaxLength);
        self.Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), CategoriaHabilidadRules.NombreMaxLength);

        return self;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
}