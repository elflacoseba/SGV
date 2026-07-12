using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// End-to-end test that proves the SGV.Web -> SGV.Api bridge forwards the
/// JWT bearer token on downstream HTTP calls. PR #65 demands an authenticated
/// principal on every cargo endpoint; without bearer propagation, the API
/// rejects every request with 401 and the cargo listing page renders
/// "No se pudo cargar el listado de cargos".
///
/// Se une a <c>[Collection("WebIntegration")]</c> para que el lease del
/// bridge quede retenido por el composite compartido (no hay factory huérfana).
/// </summary>
[Collection("WebIntegration")]
public sealed class ApiBearerTokenIntegrationTests
{
    private readonly WebIntegrationFixture _fixture;

    public ApiBearerTokenIntegrationTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_CargosIndex_WhenAuthenticated_ForwardsBearerTokenToApi()
    {
        // Arrange: a stub auth handler that issues a known JWT, plus a recording
        // handler for the cargo API so we can inspect the outbound request.
        var expectedJwt = AdminJwtTestHelper.BuildUserJwt();
        var authHandler = new StubAuthHandler(expectedJwt);
        var cargoHandler = new RecordingCargoHandler();

        await using var lease = await _fixture.CreateCargoBridgeLeaseAsync(authHandler, cargoHandler);
        var client = lease.Client;

        // Hit the page that exercises the cargo API client.
        var indexResponse = await client.GetAsync("/organizacion/cargos");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        // Assert: the cargo API call carried the JWT bearer header.
        var cargoRequest = Assert.Single(cargoHandler.Requests);
        Assert.NotNull(cargoRequest.Headers.Authorization);
        Assert.Equal("Bearer", cargoRequest.Headers.Authorization!.Scheme);
        Assert.Equal(expectedJwt, cargoRequest.Headers.Authorization.Parameter);
    }

    /// <summary>
/// Always responds with a successful login payload carrying the test JWT
/// so the Web issues a cookie ticket that stores it under "access_token".
/// </summary>
private sealed class StubAuthHandler(string accessToken) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(accessToken, new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)))
            });
    }

    /// <summary>
    /// Captures every outgoing cargo API request and answers with an empty
    /// array so the page renders successfully.
    /// </summary>
    private sealed class RecordingCargoHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CargoDto>())
            });
        }
    }
}
