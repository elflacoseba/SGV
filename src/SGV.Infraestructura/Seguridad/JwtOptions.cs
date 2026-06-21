namespace SGV.Infraestructura.Seguridad;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SGV";

    public string Audience { get; set; } = "SGV";

    public string SigningKey { get; set; } = "SGV-development-signing-key-change-before-production-2026";

    public int TokenLifetimeMinutes { get; set; } = 60;
}
