namespace SGV.Contracts.Seguridad;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SGV";

    public string Audience { get; set; } = "SGV";

    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 60;
}
