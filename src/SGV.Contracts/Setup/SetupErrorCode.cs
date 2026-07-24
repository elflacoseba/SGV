namespace SGV.Contracts.Setup;

/// <summary>
/// Códigos de error tipados para el setup one-time (issue #195). El
/// <c>SetupController</c> mapea cada valor a un HTTP status
/// (400/409/500) vía el campo <see cref="SetupError.StatusCode"/>.
/// Diseño §2.4 cubre la tabla de mapeo desde IdentityError.Code hasta
/// cada valor.
/// </summary>
public enum SetupErrorCode
{
    /// <summary>
    /// La base ya tiene al menos un usuario; el flujo one-time está cerrado.
    /// → HTTP 409.
    /// </summary>
    SetupYaCompletado,

    /// <summary>
    /// El <c>UserName</c> elegido colisiona con uno existente
    /// (IdentityError <c>DuplicateUserName</c>).
    /// → HTTP 409.
    /// </summary>
    UserNameDuplicado,

    /// <summary>
    /// El email ya está registrado (IdentityError <c>DuplicateEmail</c>).
    /// → HTTP 409.
    /// </summary>
    EmailDuplicado,

    /// <summary>
    /// El legajo elegido colisiona con uno existente en Persona
    /// (<c>PersonaServicioComandos</c> uniqueness check).
    /// → HTTP 409.
    /// </summary>
    LegajoDuplicado,

    /// <summary>
    /// La persona ya tiene un usuario Identity asociado
    /// (defensa lógica del gateway).
    /// → HTTP 409.
    /// </summary>
    PersonaConUsuario,

    /// <summary>
    /// Email con formato inválido (IdentityError <c>InvalidEmail</c>).
    /// → HTTP 400.
    /// </summary>
    EmailInvalido,

    /// <summary>
    /// UserName con caracteres inválidos (IdentityError
    /// <c>InvalidUserName</c>).
    /// → HTTP 400.
    /// </summary>
    UserNameInvalido,

    /// <summary>
    /// Contraseña incumple la política de Identity
    /// (<c>PasswordTooShort</c>, <c>PasswordRequires*</c>).
    /// → HTTP 400.
    /// </summary>
    PasswordDebil,

    /// <summary>
    /// Validación genérica de Identity (códigos no reconocidos).
    /// → HTTP 400.
    /// </summary>
    ValidacionIdentity,

    /// <summary>
    /// Error de FluentValidation previo al gateway de Identity
    /// (campos requeridos, formatos, longitudes). Acompaña
    /// <see cref="SetupCommandResult.FieldErrors"/>.
    /// → HTTP 400.
    /// </summary>
    DatosInvalidos,

    /// <summary>
    /// Falla de transacción no-Identity (MySQL down, timeout,
    /// concurrencia extrema). → HTTP 500.
    /// </summary>
    TransaccionFallida
}
