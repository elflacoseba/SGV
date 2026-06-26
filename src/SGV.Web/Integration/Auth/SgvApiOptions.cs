namespace SGV.Web.Integration.Auth;

/// <summary>
/// Configuration values for the SGV.Api integration client.
/// </summary>
public sealed class SgvApiOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SgvApi";

    /// <summary>
    /// Base URL for SGV.Api.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
