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
/// Tests del endpoint <c>POST /api/v1/setup</c> cuando una operación de
/// persistencia falla (issue #195 REQ-SETUP-002 escenario "Fallo
/// transaccional"). Verifica que el controller devuelve 500 Internal
/// Server Error con título <c>TransaccionFallida</c>.
/// </summary>
[Collection("ApiIntegration")]
public sealed class SetupTransactionalFailureTests
{
    private readonly ApiIntegrationFixture _fixture;
    public SetupTransactionalFailureTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Crear_FalloPersistencia_Devuelve500TransaccionFallida()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                crearAdminAsync: _ => SetupCommandResult.Failure(
                    new SetupError(
                        ErrorCategoria.Unexpected,
                        SetupErrorCode.TransaccionFallida,
                        "No se pudo completar la configuración inicial.",
                        StatusCode: 500))));
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

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("TransaccionFallida", problem!.Title);
        Assert.Equal(500, problem.Status);
    }
}
