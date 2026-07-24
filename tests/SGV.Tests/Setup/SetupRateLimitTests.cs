using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Setup;
using SGV.Tests.Api;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Verifica el rate limiting en <c>POST /api/v1/setup</c>: el sexto
/// request dentro de la ventana de 15 min debe recibir 429 con
/// header <c>Retry-After</c> (issue #195 REQ-SETUP-004, design §2.5).
/// </summary>
public sealed class SetupRateLimitTests
{
    [Fact]
    public async Task SextoRequest_Devuelve429ConRetryAfterHeader()
    {
        var rootFactory = new ApiWebApplicationFactory();
        var c = rootFactory.CreateClient();

        // Override ISetupServicio con un fake que devuelve Success en cada
        // llamada. El rate limiter opera en el middleware, independiente
        // del servicio.
        await using var derived = rootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                crearAdminAsync: _ => SetupCommandResult.Success(
                    new SetupResult(Guid.NewGuid(), "user-id", "admin"))));
        });

        var dc = derived.CreateClient();

        // 5 requests deben pasar (200).
        for (var i = 0; i < 5; i++)
        {
            var response = await dc.PostAsJsonAsync("/api/v1/setup", NewValidRequest());
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // 6º debe ser 429 con Retry-After.
        var sixth = await dc.PostAsJsonAsync("/api/v1/setup", NewValidRequest());
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.True(sixth.Headers.Contains("Retry-After"),
            $"Falta header Retry-After en la respuesta. Headers: {string.Join(',', sixth.Headers.Select(h => h.Key))}");
    }

    private static SetupRequest NewValidRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new SetupRequest(
            Nombres: "Operador",
            Apellidos: "RateLimit",
            Legajo: $"LEG-{suffix}",
            Email: $"rl-{suffix}@setup.test",
            UserName: $"rl-{suffix}",
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);
    }
}
