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
}