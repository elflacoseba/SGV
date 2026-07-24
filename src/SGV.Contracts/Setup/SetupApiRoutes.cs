namespace SGV.Contracts.Setup;

/// <summary>
/// Constantes de ruta y nombres de política para el setup one-time
/// (issue #195). Mantiene el patrón vigente de
/// <c>SGV.Contracts.Auth.AuthApiRoutes</c>: <see cref="Base"/> +
/// segmentos relativos + ruta absoluta pre-computada.
/// </summary>
public static class SetupApiRoutes
{
    /// <summary>Base path del setup controller.</summary>
    public const string Base = "api/v1/setup";

    /// <summary>Segmento relativo para el endpoint de estado.</summary>
    public const string StatusRelative = "status";

    /// <summary>Ruta absoluta para el endpoint de estado.</summary>
    public const string Status = "/" + Base + "/" + StatusRelative;

    /// <summary>
    /// Nombre de la política de rate limiting aplicada a
    /// <c>POST /api/v1/setup</c>. Mismo naming que las políticas
    /// existentes <c>ForgotPassword</c>/<c>ResetPassword</c>.
    /// </summary>
    public const string SetupPolicyName = "Setup";
}
