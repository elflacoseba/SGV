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
    /// El <c>CategoriaId</c> informado en el request no existe en el catálogo
    /// seed de categorías de habilidad. El backend responde con
    /// <c>400 Bad Request</c> y este código.
    /// </summary>
    public const string CategoriaHabilidadNoExiste = nameof(CategoriaHabilidadNoExiste);
}
