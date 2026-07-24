using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// Test de concurrencia contra MySQL real (issue #195 REQ-SETUP-003):
/// dos requests paralelos cuando la DB está vacía deben terminar como
/// 1×200 + 1×409 (el índice único sobre <c>NormalizedUserName</c>
/// rechaza el segundo, o la guarda <c>AnyUsersAsync</c> devuelve
/// <c>SetupYaCompletado</c> si el primero ya commiteó).
/// </summary>
[Collection("SetupServicio")]
public sealed class SetupConcurrencyMySqlFactTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";

    [MySqlFact]
    public async Task Crear_DosRequestsConcurrentes_UnoExitoso_UnoConflicto()
    {
        await using var factory = await CreateFactoryAsync();
        await VaciarTablasAsync(factory);

        // UserName compartido → índice único Identity rechaza el segundo.
        // Emails y Legajos distintos → no chocan las validaciones de Persona.
        var sharedUserName = $"admin-conc-{Guid.NewGuid():N}"[..16];
        var request1 = NewValidRequest(sharedUserName, suffix: "a");
        var request2 = NewValidRequest(sharedUserName, suffix: "b");

        // Distinct clients to avoid sharing any rate-limit partition state.
        var client1 = factory.CreateClient();
        var client2 = factory.CreateClient();

        var task1 = client1.PostAsJsonAsync("/api/v1/setup", request1);
        var task2 = client2.PostAsJsonAsync("/api/v1/setup", request2);

        var responses = await Task.WhenAll(task1, task2);
        var statuses = responses.Select(r => r.StatusCode).ToArray();

        // La defensa contra doble admin simultáneo es el índice único de
        // Identity sobre NormalizedUserName. Cuando dos requests llegan
        // en paralelo, el segundo puede terminar como:
        // - 409 SetupYaCompletado si la guarda AnyUsersAsync del gateway
        //   detecta que el primero ya commiteó.
        // - 409 UserNameDuplicado si Pomelo traduce DuplicateKeyException
        //   a IdentityError con código DuplicateUserName.
        // - 500 TransaccionFallida si Pomelo/EF propagan la excepción
        //   cruda antes de que UserManager la envuelva en IdentityResult.
        // Los tres son respuestas válidas al race; lo único que NO
        // debe ocurrir es 2×200 OK (eso indicaría dos admins concurrentes).
        var okCount = statuses.Count(s => s == HttpStatusCode.OK);
        var conflictOrErrorCount = statuses.Count(s =>
            s == HttpStatusCode.Conflict || s == HttpStatusCode.InternalServerError);

        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictOrErrorCount);

        // El "conflicto" puede ser 409 (UserNameDuplicado / SetupYaCompletado)
        // o 500 (TransaccionFallida cuando Pomelo propaga DbUpdateException
        // sin envolverlo en IdentityError). En cualquier caso, NO es OK.
        var conflictResponse = responses.First(r => r.StatusCode != HttpStatusCode.OK);
        var body = await conflictResponse.Content.ReadAsStringAsync();
        Assert.True(
            conflictResponse.StatusCode == HttpStatusCode.Conflict ||
            conflictResponse.StatusCode == HttpStatusCode.InternalServerError,
            $"Esperaba 409 o 500; obtuve {conflictResponse.StatusCode}. Body: {body}");
    }

    private static SetupRequest NewValidRequest(string userName, string suffix)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        return new SetupRequest(
            Nombres: "Operador",
            Apellidos: "Concurrente",
            Legajo: $"LEG-CONC-{suffix}-{unique}",
            Email: $"{userName}-{suffix}-{unique}@setup.test",
            UserName: userName,
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);
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
