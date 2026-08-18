namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Categorizes command-side failures for Persona operations.
///
/// <para>
/// D-PE-02 (release-readiness módulo Personas): las variantes 0/1/2
/// (<c>NotFound</c>/<c>Conflict</c>/<c>Validation</c>) preservan los ordinales
/// históricos para no romper callers existentes que dependan de
/// <c>(int)PersonaErrorType.X</c>. Las variantes 3-6 (<c>Unauthorized</c>,
/// <c>Forbidden</c>, <c>Transport</c>, <c>Unexpected</c>) se alinean
/// 1-a-1 con <see cref="SGV.Contracts.Comun.ErrorCategoria"/> y siguen el
/// precedente del housekeeping pre-release de Cargos (D-CH-04, PR #287).
/// </para>
/// <para>
/// Los call sites vigentes ya discriminan por
/// <see cref="SGV.Contracts.Comun.ErrorCategoria"/> directamente; este
/// enum queda como campo legacy preservado por source-compat (ver
/// <see cref="SGV.Contracts.Comun.ErrorCategoriaMappers.ToTipoPersona"/>).
/// </para>
/// </summary>
public enum PersonaErrorType
{
    NotFound = 0,
    Conflict = 1,
    Validation = 2,
    Unauthorized = 3,
    Forbidden = 4,
    Transport = 5,
    Unexpected = 6
}