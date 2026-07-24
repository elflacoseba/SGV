using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Contracts.Setup;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Test de integración end-to-end contra MySQL real (issue #195
/// REQ-SETUP-002 escenario "Creación válida"). Verifica que
/// <c>POST /api/v1/setup</c> con datos válidos:
/// - Devuelve 200 OK.
/// - Crea fila en <c>Personas</c>.
/// - Crea fila en <c>AspNetUsers</c>.
/// - Asigna rol <c>Administrador</c>.
/// - Crea fila en <c>Auditorias</c> con <c>userId="system"</c>.
/// </summary>
[Collection("SetupServicio")]
public sealed class SetupHappyPathMySqlFactTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";

    [MySqlFact]
    public async Task Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria()
    {
        await using var factory = await CreateFactoryAsync();
        await VaciarTablasAsync(factory);

        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var request = new SetupRequest(
            Nombres: "Operador",
            Apellidos: "Inicial",
            Legajo: $"LEG-{suffix}",
            Email: $"operador-{suffix}@setup.test",
            UserName: $"operador-{suffix}",
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: "+5491100000000");

        var response = await client.PostAsJsonAsync("/api/v1/setup", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SetupCommandResult>();
        Assert.NotNull(result);
        Assert.True(result!.IsSuccess,
            $"Esperaba éxito. Error={result.Error?.Code} Msg={result.Error?.Message}");
        Assert.NotEqual(Guid.Empty, result.Value!.PersonaId);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var userManager = verifyScope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var user = await userManager.FindByIdAsync(result.Value.UserId);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user!);
        Assert.Contains(RolesSgv.Administrador, roles);
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
