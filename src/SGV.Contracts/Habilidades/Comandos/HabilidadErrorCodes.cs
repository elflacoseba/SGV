namespace SGV.Contracts.Habilidades.Comandos;

/// <summary>
/// Códigos de error canónicos devueltos por la API de habilidades.
/// Los valores de estas constantes son el contrato wire entre el backend
/// (<c>SGV.Api</c>) y el frontend (<c>SGV.Web</c>): cualquier cambio requiere
/// actualizar ambos extremos simultáneamente.
/// </summary>
public static class HabilidadErrorCodes
{
    /// <summary>
    /// Validación de shape (FluentValidation) o de dominio falló. El backend
    /// responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string DatosInvalidos = nameof(DatosInvalidos);

    /// <summary>
    /// La habilidad solicitada no existe (búsqueda por <c>id</c>). El backend
    /// responde con <c>404 Not Found</c> y este código.
    /// </summary>
    public const string HabilidadNoEncontrada = nameof(HabilidadNoEncontrada);

    /// <summary>
    /// Conflicto: otra habilidad activa ya usa el mismo <c>Codigo</c>. El
    /// backend responde con <c>409 Conflict</c> y este código.
    /// </summary>
    public const string CodigoDuplicado = nameof(CodigoDuplicado);

    /// <summary>
    /// La desactivación solicitada es inválida (regla de dominio). El backend
    /// responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string DesactivacionInvalida = nameof(DesactivacionInvalida);

    /// <summary>
    /// La reactivación solicitada es inválida (regla de dominio). El backend
    /// responde con <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string ReactivacionInvalida = nameof(ReactivacionInvalida);

    /// <summary>
    /// El <c>CategoriaId</c> informado en el request no existe en el catálogo
    /// seed de categorías de habilidad. El backend responde con
    /// <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string CategoriaHabilidadNoExiste = nameof(CategoriaHabilidadNoExiste);
}
