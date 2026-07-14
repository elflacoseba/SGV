namespace SGV.Web.Integration.Health;

/// <summary>
/// Constants for the named HTTP client used by <see cref="SgvApiUpstreamHealthCheck"/>
/// to probe the SGV API upstream.
/// </summary>
public static class SgvApiHealthProbeHttpClient
{
    /// <summary>
    /// Named client identifier for the health probe.
    /// Registered without <c>ApiBearerTokenHandler</c> — the probe is anonymous
    /// and must not carry user authentication context.
    /// </summary>
    public const string Name = "SgvApiHealthProbe";
}
