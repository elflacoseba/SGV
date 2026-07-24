using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Comun;
using SGV.Contracts.Setup;
using SGV.Tests.Api;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Tests del endpoint <c>POST /api/v1/setup</c> cuando el setup ya fue
/// completado (issue #195 REQ-SETUP-002 escenario "Setup ya completado").
/// Verifica:
/// - Devuelve 409 Conflict.
/// - El código es <see cref="SetupErrorCode.SetupYaCompletado"/>.
/// - El campo <c>Title</c> del ProblemDetails contiene el código.
/// </summary>
[Collection("ApiIntegration")]
public sealed class SetupAlreadyCompletedTests
{
    private readonly ApiIntegrationFixture _fixture;
    public SetupAlreadyCompletedTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Crear_DBTieneUsuarios_Devuelve409SetupYaCompletado()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                crearAdminAsync: _ => SetupCommandResult.Failure(
                    new SetupError(
                        ErrorCategoria.Conflict,
                        SetupErrorCode.SetupYaCompletado,
                        "La configuración inicial ya fue completada.",
                        StatusCode: 409))));
        });
        var client = factory.CreateClient();

        var request = new SetupRequest(
            Nombres: "Admin",
            Apellidos: "Seed",
            Legajo: null,
            Email: "admin@test.com",
            UserName: "admin",
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);

        var response = await client.PostAsJsonAsync("/api/v1/setup", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("SetupYaCompletado", problem!.Title);
        Assert.Equal(409, problem.Status);
    }
}
