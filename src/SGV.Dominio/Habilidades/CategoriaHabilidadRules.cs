namespace SGV.Dominio.Habilidades;

/// <summary>
/// Single source of truth para las constantes de reglas de negocio
/// (longitudes) de <see cref="CategoriaHabilidad"/>. Las constantes las
/// comparten la entidad, los validadores de aplicación y los tests.
/// </summary>
public static class CategoriaHabilidadRules
{
    /// <summary>
    /// Longitud máxima del <c>Codigo</c> de una CategoriaHabilidad.
    /// Alineado con <c>TipoDocumentoRules.CodigoMaxLength</c> por paridad
    /// con el resto de catálogos inmutables seed-only.
    /// </summary>
    public const int CodigoMaxLength = 50;

    /// <summary>
    /// Longitud máxima del <c>Nombre</c> de una CategoriaHabilidad.
    /// </summary>
    public const int NombreMaxLength = 100;
}