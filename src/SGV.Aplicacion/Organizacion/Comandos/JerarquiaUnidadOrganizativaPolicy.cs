namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Pure policy that validates hierarchy relationships and vigencia containment
/// for organizational units based on a seed type matrix.
///
/// The matrix defines which parent tipo codes are allowed for each child tipo code.
/// Changes to the matrix must go through explicit specification and migration.
/// </summary>
public sealed class JerarquiaUnidadOrganizativaPolicy
{
    // Matriz explícita: código de tipo hija → códigos de tipo padre permitidos.
    // Array vacío significa que el tipo solo puede ser raíz (sin padre).
    private static readonly Dictionary<string, string[]> RelacionesPermitidas = new()
    {
        ["Institucion"]  = [],                       // solo raíz
        ["Facultad"]     = ["Institucion"],
        ["Secretaria"]   = ["Institucion", "Facultad"],
        ["Direccion"]    = ["Institucion", "Facultad", "Secretaria"],
        ["Departamento"] = ["Facultad", "Direccion"],
        ["Division"]     = ["Direccion", "Departamento"],
        ["Area"]         = ["Secretaria", "Direccion", "Departamento", "Division"],
    };

    /// <summary>
    /// Returns true when a unit of <paramref name="tipoCodigoHija"/> may be placed under
    /// a parent of <paramref name="tipoCodigoPadre"/>. A null parent means root placement.
    /// </summary>
    public bool EsRelacionPermitida(string tipoCodigoHija, string? tipoCodigoPadre)
    {
        if (!RelacionesPermitidas.TryGetValue(tipoCodigoHija, out var padresPermitidos))
            return false;

        // Root types (empty array) are only allowed without a parent
        if (tipoCodigoPadre is null)
            return padresPermitidos.Length == 0;

        // Non-root types require a matching parent in the allowed list
        return padresPermitidos.Length > 0 && padresPermitidos.Contains(tipoCodigoPadre);
    }

    /// <summary>
    /// Returns true when the child's validity range (if specified) is contained within
    /// the parent's validity range. When either side has no dates, containment is
    /// considered valid (open-ended).
    /// </summary>
    public bool EsVigenciaContenida(
        DateOnly? hijaDesde, DateOnly? hijaHasta,
        DateOnly? padreDesde, DateOnly? padreHasta)
    {
        // If the child has no dates, it's always contained
        if (!hijaDesde.HasValue && !hijaHasta.HasValue)
            return true;

        // If the parent has no dates, any child dates are valid
        if (!padreDesde.HasValue && !padreHasta.HasValue)
            return true;

        // Child's start must not be before parent's start
        if (hijaDesde.HasValue && padreDesde.HasValue && hijaDesde.Value < padreDesde.Value)
            return false;

        // Child's end must not be after parent's end
        if (hijaHasta.HasValue && padreHasta.HasValue && hijaHasta.Value > padreHasta.Value)
            return false;

        return true;
    }
}
