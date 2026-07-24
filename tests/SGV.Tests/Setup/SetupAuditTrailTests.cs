using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Setup;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Verifica que la creación exitosa del primer admin deja una fila en
/// <c>Auditorias</c> con <c>UserId="system"</c>,
/// <c>EntityName="SetupInicial"</c> y
/// <c>Operation="AltaPrimerAdministrador"</c> (issue #195 REQ-SETUP-004).
/// </summary>
[Collection("SetupServicio")]
public sealed class SetupAuditTrailTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";

    [MySqlFact]
    public async Task Crear_Exitoso_RegistraAuditoriaConUserIdSystem()
    {
        await using var factory = await CreateFactoryAsync();
        await VaciarTablasAsync(factory);

        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var request = new SetupRequest(
            Nombres: "Operador",
            Apellidos: "Auditable",
            Legajo: $"LEG-{suffix}",
            Email: $"audit-{suffix}@setup.test",
            UserName: $"audit-{suffix}",
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);

        var response = await client.PostAsJsonAsync("/api/v1/setup", request);
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var audit = await db.Auditorias
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.EntityName == "SetupInicial" &&
                a.Operation == "AltaPrimerAdministrador");

        Assert.NotNull(audit);
        Assert.Equal("system", audit!.UserId);
    }

    private static async Task<JwtRealWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task VaciarTablasAsync(JwtRealWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `Auditorias`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUserRoles`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUsers`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `Personas`");
        await db.SaveChangesAsync();
    }
}
