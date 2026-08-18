namespace SGV.Aplicacion.Seguridad;

/// <summary>
/// Implementación nula de <see cref="IUsuarioActual"/> que no resuelve
/// a un usuario. Se usa como default en constructores de back-compat
/// (issue #202) para que los tests pre-existentes no necesiten cablear
/// un usuario actual; el código de producción real resuelve
/// <c>UsuarioActualHttpContext</c> desde DI.
/// </summary>
internal sealed class NullUsuarioActual : IUsuarioActual
{
    public string? UserId => null;

    public Guid? PersonaId => null;

    public IReadOnlyCollection<string> Roles => [];

    public Guid? CorrelationId => null;

    /// <summary>
    /// Singleton reusado por los convenience constructors de los
    /// servicios para mantener back-compat con los call sites que no
    /// cablean un principal explícito (issue #202, cambio
    /// <c>vacantes-hardening</c> D-1).
    /// </summary>
    public static NullUsuarioActual Instance { get; } = new();
}