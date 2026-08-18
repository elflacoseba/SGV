namespace SGV.Dominio.Habilidades;

/// <summary>
/// Constantes de reglas de negocio (longitudes y límites) compartidas por la
/// entidad <see cref="Habilidad"/>, sus validadores de aplicación y los
/// tests. Single source of truth para evitar magic numbers repetidos.
/// </summary>
public static class HabilidadRules
{
    /// <summary>
    /// Longitud máxima del <c>Codigo</c> de una Habilidad. Aplicada tanto
    /// por la entidad (<see cref="ValidacionesDominio"/>) como por el
    /// validador FluentValidation de la capa de aplicación.
    /// </summary>
    public const int CodigoMaxLength = 50;

    /// <summary>
    /// Longitud máxima del <c>Nombre</c> de una Habilidad. Aplicada tanto
    /// por la entidad (<see cref="ValidacionesDominio"/>) como por el
    /// validador FluentValidation de la capa de aplicación.
    /// </summary>
    public const int NombreMaxLength = 200;

    /// <summary>
    /// Longitud máxima de la <c>Descripcion</c> de una Habilidad. Aplicada
    /// tanto por la entidad (<see cref="ValidacionesDominio"/>) como por el
    /// validador FluentValidation de la capa de aplicación. El campo es
    /// opcional; la regla sólo aplica cuando hay valor.
    /// </summary>
    public const int DescripcionMaxLength = 1000;
}