using SGV.Aplicacion.Seguridad;

namespace SGV.Api.Seguridad;

/// <summary>
/// Anonymous user adapter for the API layer. Used to satisfy audit interceptor
/// dependencies when no authenticated user context exists. Returns a fixed
/// correlation ID per request scope.
/// </summary>
public sealed class UsuarioActualAnonimo : IUsuarioActual
{
    public UsuarioActualAnonimo()
    {
        CorrelationId = Guid.NewGuid();
    }

    /// <summary>
    /// No authenticated user — returns null.
    /// </summary>
    public string? UserId => null;

    /// <summary>
    /// Correlation identifier for the current request scope.
    /// </summary>
    public Guid? CorrelationId { get; }
}
