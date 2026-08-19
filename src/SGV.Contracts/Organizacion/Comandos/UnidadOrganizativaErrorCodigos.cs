namespace SGV.Contracts.Organizacion.Comandos;

/// <summary>
/// Códigos de error canónicos devueltos por la API de unidades organizativas.
/// Los valores de estas constantes son el contrato wire entre el backend
/// (<c>SGV.Api</c>) y el frontend (<c>SGV.Web</c>): cualquier cambio requiere
/// actualizar ambos extremos simultáneamente.
/// </summary>
/// <remarks>
/// H-C3 (housekeeping release-readiness UO+Organigrama): centraliza los
/// literales que vivían como magic strings en
/// <c>UnidadOrganizativaServicioComandos</c>. Sin esto, un typo no rompía
/// compilación y solo se detectaba si un test comparaba el literal exacto.
/// </remarks>
public static class UnidadOrganizativaErrorCodigos
{
    /// <summary>
    /// Validación de shape (FluentValidation) o de dominio falló. El backend
    /// responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string DatosInvalidos = nameof(DatosInvalidos);

    /// <summary>
    /// La unidad organizativa solicitada no existe (búsqueda por <c>id</c>).
    /// El backend responde con <c>404 Not Found</c> y este código.
    /// </summary>
    public const string UnidadNoEncontrada = nameof(UnidadNoEncontrada);

    /// <summary>
    /// El padre referenciado por la unidad no existe. El backend responde
    /// con <c>404 Not Found</c> y este código.
    /// </summary>
    public const string UnidadPadreNoEncontrada = nameof(UnidadPadreNoEncontrada);

    /// <summary>
    /// El tipo de unidad organizativa referenciado no existe en el catálogo
    /// seed. El backend responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string TipoUnidadNoExiste = nameof(TipoUnidadNoExiste);

    /// <summary>
    /// Conflicto: ya existe una unidad organizativa activa con el mismo
    /// <c>Codigo</c> (o violación de <c>IX_UnidadesOrganizativas_ActiveCodigoUnique</c>
    /// por carrera con <c>ExistsActiveCodeAsync</c>). El backend responde con
    /// <c>409 Conflict</c> y este código.
    /// </summary>
    public const string CodigoDuplicado = nameof(CodigoDuplicado);

    /// <summary>
    /// Conflicto: la operación forma un ciclo en la jerarquía
    /// (auto-parent, descendiente como padre, o SIGNAL 1644 del trigger
    /// anti-ciclos, issue #277). El backend responde con <c>409 Conflict</c>
    /// y este código. El mensaje del SIGNAL es <c>'CicloJerarquico'</c>
    /// (constante en la migración <c>20260816203122</c>) y el servicio lo
    /// mapea por contenido del <c>InnerException.Message</c>.
    /// </summary>
    public const string CicloJerarquico = nameof(CicloJerarquico);

    /// <summary>
    /// Conflicto: la unidad organizativa tiene hijas activas. El backend
    /// responde con <c>409 Conflict</c> y este código.
    /// </summary>
    public const string UnidadConHijasActivas = nameof(UnidadConHijasActivas);

    /// <summary>
    /// Conflicto: la unidad organizativa tiene puestos activos asociados.
    /// El backend responde con <c>409 Conflict</c> y este código.
    /// </summary>
    public const string UnidadConPuestosActivos = nameof(UnidadConPuestosActivos);

    /// <summary>
    /// Conflicto: no se puede reactivar una unidad cuyo padre está inactivo
    /// o eliminado. El backend responde con <c>409 Conflict</c> y este código.
    /// </summary>
    public const string PadreInactivo = nameof(PadreInactivo);

    /// <summary>
    /// La reactivación solicitada es inválida (regla de dominio). El backend
    /// responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string ReactivacionInvalida = nameof(ReactivacionInvalida);

    /// <summary>
    /// Catch-all para violaciones de integridad de BD que no son ciclos ni
    /// códigos duplicados (FK violations, etc). El backend responde con
    /// <c>409 Conflict</c> y este código, sin exponer detalles de BD al cliente.
    /// </summary>
    public const string RestriccionDeIntegridad = nameof(RestriccionDeIntegridad);
}
