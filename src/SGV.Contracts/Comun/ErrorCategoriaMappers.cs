using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Contracts.Comun;

/// <summary>
/// Mapeos explícitos (nombre-a-nombre) entre los enums <c>*ErrorType</c>
/// vigentes y la taxonomía común <see cref="ErrorCategoria"/>.
///
/// <para>
/// <b>NO se usa conversión por ordinal.</b> Los enums vigentes tienen
/// ordenamientos divergentes (p.ej. <c>CargoSkillErrorType.Validation = 1</c>
/// mientras <c>ErrorCategoria.Validation = 2</c>, con <c>Conflict = 1</c>).
/// Toda traducción se hace por nombre con <c>switch</c> expressions
/// exhaustivos para preservar el significado semántico.
/// </para>
/// <para>
/// Los enums <c>*ErrorType</c> vigentes están marcados <c>[Obsolete]</c>
/// durante este change y se eliminarán al archivar la
/// capability <c>commandresult-error-taxonomy</c>. Estos mappers actúan
/// como puente entre los call sites que aún ramifican por el enum viejo y
/// el nuevo <see cref="ErrorCategoria"/>.
/// </para>
/// </summary>
public static class ErrorCategoriaMappers
{
    // ============================================================
    // HabilidadErrorType
    // ============================================================

    /// <summary>
    /// Traduce <see cref="HabilidadErrorType"/> al <see cref="ErrorCategoria"/>
    /// equivalente. <c>HabilidadErrorType.Infrastructure</c> representa
    /// fallos de transporte upstream y mapea a <see cref="ErrorCategoria.Transport"/>.
    /// </summary>
    public static ErrorCategoria ToCategoria(HabilidadErrorType type) => type switch
    {
        HabilidadErrorType.NotFound => ErrorCategoria.NotFound,
        HabilidadErrorType.Conflict => ErrorCategoria.Conflict,
        HabilidadErrorType.Validation => ErrorCategoria.Validation,
        HabilidadErrorType.Infrastructure => ErrorCategoria.Transport,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"HabilidadErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al <see cref="HabilidadErrorType"/>
    /// equivalente. Las categorías sin equivalente (<c>Unauthorized</c>,
    /// <c>Forbidden</c>, <c>Unexpected</c>) lanzan
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <remarks>
    /// Nombrado por dominio (<c>ToTipoHabilidad</c>, <c>ToTipoCargo</c>, etc.)
    /// porque C# no permite overloads que difieren solo en tipo de retorno.
    /// </remarks>
    public static HabilidadErrorType ToTipoHabilidad(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => HabilidadErrorType.NotFound,
        ErrorCategoria.Conflict => HabilidadErrorType.Conflict,
        ErrorCategoria.Validation => HabilidadErrorType.Validation,
        ErrorCategoria.Transport => HabilidadErrorType.Infrastructure,
        ErrorCategoria.Unauthorized => throw new NotSupportedException(
            "HabilidadErrorType no tiene variante Unauthorized."),
        ErrorCategoria.Forbidden => throw new NotSupportedException(
            "HabilidadErrorType no tiene variante Forbidden."),
        ErrorCategoria.Unexpected => throw new NotSupportedException(
            "HabilidadErrorType no tiene variante Unexpected."),
    };

    // ============================================================
    // CargoErrorType
    // ============================================================

    /// <summary>
    /// Traduce <see cref="CargoErrorType"/> al <see cref="ErrorCategoria"/>
    /// equivalente.
    /// </summary>
    public static ErrorCategoria ToCategoria(CargoErrorType type) => type switch
    {
        CargoErrorType.NotFound => ErrorCategoria.NotFound,
        CargoErrorType.Conflict => ErrorCategoria.Conflict,
        CargoErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"CargoErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al <see cref="CargoErrorType"/>
    /// equivalente. <c>Transport</c> y <c>Unexpected</c> colapsan a
    /// <c>Validation</c> por compat con la API histórica del cliente
    /// (los clientes <c>CargoApiClient</c> previos colapsaban 5xx en
    /// <c>Validation</c>); las categorías <c>Unauthorized</c> y
    /// <c>Forbidden</c> no tienen equivalente y lanzan
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public static CargoErrorType ToTipoCargo(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => CargoErrorType.NotFound,
        ErrorCategoria.Conflict => CargoErrorType.Conflict,
        ErrorCategoria.Validation => CargoErrorType.Validation,
        ErrorCategoria.Transport => CargoErrorType.Validation,
        ErrorCategoria.Unexpected => CargoErrorType.Validation,
        ErrorCategoria.Unauthorized => throw new NotSupportedException(
            "CargoErrorType no tiene variante Unauthorized."),
        ErrorCategoria.Forbidden => throw new NotSupportedException(
            "CargoErrorType no tiene variante Forbidden."),
    };

    // ============================================================
    // PuestoErrorType
    // ============================================================

    /// <summary>
    /// Traduce <see cref="PuestoErrorType"/> al <see cref="ErrorCategoria"/>
    /// equivalente.
    /// </summary>
    public static ErrorCategoria ToCategoria(PuestoErrorType type) => type switch
    {
        PuestoErrorType.NotFound => ErrorCategoria.NotFound,
        PuestoErrorType.Conflict => ErrorCategoria.Conflict,
        PuestoErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"PuestoErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al <see cref="PuestoErrorType"/>
    /// equivalente. <c>Transport</c> y <c>Unexpected</c> colapsan a
    /// <c>Validation</c> por compat con la API histórica del cliente
    /// <c>PuestosApiClient</c>; <c>Unauthorized</c> y <c>Forbidden</c> no
    /// tienen equivalente y lanzan <see cref="NotSupportedException"/>.
    /// </summary>
    public static PuestoErrorType ToTipoPuesto(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => PuestoErrorType.NotFound,
        ErrorCategoria.Conflict => PuestoErrorType.Conflict,
        ErrorCategoria.Validation => PuestoErrorType.Validation,
        ErrorCategoria.Transport => PuestoErrorType.Validation,
        ErrorCategoria.Unexpected => PuestoErrorType.Validation,
        ErrorCategoria.Unauthorized => throw new NotSupportedException(
            "PuestoErrorType no tiene variante Unauthorized."),
        ErrorCategoria.Forbidden => throw new NotSupportedException(
            "PuestoErrorType no tiene variante Forbidden."),
    };

    // ============================================================
    // UnidadOrganizativaErrorType
    // ============================================================

    /// <summary>
    /// Traduce <see cref="UnidadOrganizativaErrorType"/> al
    /// <see cref="ErrorCategoria"/> equivalente.
    /// </summary>
    public static ErrorCategoria ToCategoria(UnidadOrganizativaErrorType type) => type switch
    {
        UnidadOrganizativaErrorType.NotFound => ErrorCategoria.NotFound,
        UnidadOrganizativaErrorType.Conflict => ErrorCategoria.Conflict,
        UnidadOrganizativaErrorType.Validation => ErrorCategoria.Validation,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"UnidadOrganizativaErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al
    /// <see cref="UnidadOrganizativaErrorType"/> equivalente.
    /// <c>Transport</c> y <c>Unexpected</c> colapsan a <c>Validation</c>
    /// por compat con la API histórica del cliente
    /// <c>UnidadOrganizativaApiClient</c>; <c>Unauthorized</c> y
    /// <c>Forbidden</c> no tienen equivalente y lanzan
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public static UnidadOrganizativaErrorType ToTipoUnidadOrganizativa(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => UnidadOrganizativaErrorType.NotFound,
        ErrorCategoria.Conflict => UnidadOrganizativaErrorType.Conflict,
        ErrorCategoria.Validation => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Transport => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Unexpected => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Unauthorized => throw new NotSupportedException(
            "UnidadOrganizativaErrorType no tiene variante Unauthorized."),
        ErrorCategoria.Forbidden => throw new NotSupportedException(
            "UnidadOrganizativaErrorType no tiene variante Forbidden."),
    };

    // ============================================================
    // CargoSkillErrorType (alineado 1-a-1 con ErrorCategoria)
    // ============================================================

    /// <summary>
    /// Traduce <see cref="CargoSkillErrorType"/> al
    /// <see cref="ErrorCategoria"/> equivalente. La relación es 1-a-1
    /// para todas las variantes; el mapeo se hace por nombre (no por
    /// ordinal) porque los ordinales no coinciden — ver
    /// <c>CargoSkillErrorType_Validation_MapsToCategoriaValidation_NotConflict</c>.
    /// </summary>
    public static ErrorCategoria ToCategoria(CargoSkillErrorType type) => type switch
    {
        CargoSkillErrorType.NotFound => ErrorCategoria.NotFound,
        CargoSkillErrorType.Validation => ErrorCategoria.Validation,
        CargoSkillErrorType.Conflict => ErrorCategoria.Conflict,
        CargoSkillErrorType.Unauthorized => ErrorCategoria.Unauthorized,
        CargoSkillErrorType.Forbidden => ErrorCategoria.Forbidden,
        CargoSkillErrorType.Transport => ErrorCategoria.Transport,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"CargoSkillErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al <see cref="CargoSkillErrorType"/>
    /// equivalente. <c>Unexpected</c> no tiene equivalente y lanza
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public static CargoSkillErrorType ToTipoCargoSkill(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => CargoSkillErrorType.NotFound,
        ErrorCategoria.Validation => CargoSkillErrorType.Validation,
        ErrorCategoria.Conflict => CargoSkillErrorType.Conflict,
        ErrorCategoria.Unauthorized => CargoSkillErrorType.Unauthorized,
        ErrorCategoria.Forbidden => CargoSkillErrorType.Forbidden,
        ErrorCategoria.Transport => CargoSkillErrorType.Transport,
        ErrorCategoria.Unexpected => throw new NotSupportedException(
            "CargoSkillErrorType no tiene variante Unexpected."),
    };

    // ============================================================
    // UsuarioErrorType
    // ============================================================

    /// <summary>
    /// Traduce <see cref="UsuarioErrorType"/> al <see cref="ErrorCategoria"/>
    /// equivalente.
    /// </summary>
    public static ErrorCategoria ToCategoria(UsuarioErrorType type) => type switch
    {
        UsuarioErrorType.NotFound => ErrorCategoria.NotFound,
        UsuarioErrorType.Conflict => ErrorCategoria.Conflict,
        UsuarioErrorType.Validation => ErrorCategoria.Validation,
        UsuarioErrorType.Unauthorized => ErrorCategoria.Unauthorized,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type,
            $"UsuarioErrorType value '{type}' has no categoria mapping."),
    };

    /// <summary>
    /// Traduce <see cref="ErrorCategoria"/> al <see cref="UsuarioErrorType"/>
    /// equivalente. <c>Transport</c> y <c>Unexpected</c> colapsan a
    /// <c>Validation</c> por compat con el cliente <c>AuthApiClient</c>;
    /// <c>Forbidden</c> no tiene equivalente y lanza
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public static UsuarioErrorType ToTipoUsuario(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => UsuarioErrorType.NotFound,
        ErrorCategoria.Conflict => UsuarioErrorType.Conflict,
        ErrorCategoria.Validation => UsuarioErrorType.Validation,
        ErrorCategoria.Unauthorized => UsuarioErrorType.Unauthorized,
        ErrorCategoria.Transport => UsuarioErrorType.Validation,
        ErrorCategoria.Unexpected => UsuarioErrorType.Validation,
        ErrorCategoria.Forbidden => throw new NotSupportedException(
            "UsuarioErrorType no tiene variante Forbidden."),
    };
}
