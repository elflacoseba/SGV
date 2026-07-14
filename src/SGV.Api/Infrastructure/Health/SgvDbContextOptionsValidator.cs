using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Persistencia;

namespace SGV.Api.Infrastructure.Health;

/// <summary>
/// Validates <c>ConnectionStrings:SgvDatabase</c> at startup via
/// <see cref="IValidateOptions{T}"/>. Registered as singleton and triggered
/// by <c>ValidateOnStart</c> so the API fails loud when the connection string
/// is missing or malformed, matching the behaviour of JWT and SgvApiOptions
/// validation already in place.
/// </summary>
public sealed class SgvDbContextOptionsValidator(IConfiguration configuration)
    : IValidateOptions<DbContextOptions<SgvDbContext>>
{
    public ValidateOptionsResult Validate(string? name, DbContextOptions<SgvDbContext> options)
    {
        var connectionString = configuration.GetConnectionString("SgvDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail(
                "Debe configurar ConnectionStrings:SgvDatabase antes de iniciar la API.");
        }

        if (!connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || !connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "ConnectionStrings:SgvDatabase inválida: debe incluir Server= y Database=.");
        }

        if (!connectionString.Contains("Connection Timeout=", StringComparison.OrdinalIgnoreCase))
        {
            // Not a hard failure — the host continues but operators should add
            // Connection Timeout to avoid long hangs during AutoDetect.
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Success;
    }
}
