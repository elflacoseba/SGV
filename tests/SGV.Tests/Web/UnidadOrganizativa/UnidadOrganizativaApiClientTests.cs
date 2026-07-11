using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Web.Integration.Organizacion;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.UnidadOrganizativa;

/// <summary>
/// Unit tests for the typed <see cref="UnidadOrganizativaApiClient"/> focused
/// on the graceful-degradation contract of <c>ToCommandResultAsync</c>.
///
/// Regression net for issue #102: the unexpected/unmapped-status branch must
/// return a typed <see cref="UnidadOrganizativaCommandResult"/> failure
/// instead of throwing via <c>EnsureSuccessStatusCode()</c>. A thrown
/// exception here would bubble up to the Razor Page and break the elegant
/// error surface the other typed clients (Cargo, Puesto, Habilidad) already
/// guarantee.
/// </summary>
public class UnidadOrganizativaApiClientTests
{
    [Fact]
    public async Task CreateAsync_UnexpectedStatusWithNonJsonBody_ReturnsTypedFailureWithoutThrowing()
    {
        // 500 con body HTML: ni éxito ni un status mapeado (400/404/409).
        // Antes de la corrección esto pasaba por EnsureSuccessStatusCode y
        // tiraba HttpRequestException. Ahora degrada a un Failure tipado.
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<html>boom</html>", Encoding.UTF8, "text/html")
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.CreateAsync(NewRequest());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, result.Error!.Type);
        Assert.Equal("Unexpected", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_UnexpectedStatusWithProblemDetails_PreservesTitleAndDetail()
    {
        // 401 con ProblemDetails: status no mapeado explícitamente, pero el
        // backend envió title/detail. El fallback debe preservarlos en vez de
        // perderlos (o de tirar excepción).
        var problem = new ProblemDetails
        {
            Status = 401,
            Title = "NoAutorizado",
            Detail = "El token expiró."
        };
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(problem)
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.UpdateAsync(Guid.NewGuid(), NewUpdateRequest());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, result.Error!.Type);
        Assert.Equal("NoAutorizado", result.Error.Code);
        Assert.Equal("El token expiró.", result.Error.Message);
    }

    private static CrearUnidadOrganizativaRequest NewRequest() =>
        new("UO-001", "Dirección General", Guid.NewGuid());

    private static ActualizarUnidadOrganizativaRequest NewUpdateRequest() =>
        new("Dirección General", Guid.NewGuid());

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
}
