using System.Net;
using Xunit;

namespace SGV.Tests.Web._Shared;

/// <summary>
/// Verifies the contract of the shared helper used by the typed
/// <c>HabilidadApiClient</c> and <c>CargoApiClient</c> transport-failure suites.
/// </summary>
public class HttpClientExceptionScenariosTests
{
    [Fact]
    public void TransportExceptionData_HasTwoRows_ForTaskCanceledAndHttpRequest()
    {
        var rows = HttpClientExceptionScenarios.TransportExceptionData.ToList();

        Assert.Equal(2, rows.Count);
        var types = rows.Select(row => (Type)row[2]).ToArray();
        Assert.Contains(typeof(TaskCanceledException), types);
        Assert.Contains(typeof(HttpRequestException), types);
    }

    [Fact]
    public async Task NewHandlerThrowing_InvokesFactoryInSendAsync_AndPropagatesException()
    {
        var factoryInvocations = 0;
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(() =>
        {
            factoryInvocations++;
            return new HttpRequestException("simulated transport failure");
        });
        var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

        await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://api.test/skills"),
            CancellationToken.None));

        Assert.Equal(1, factoryInvocations);
    }

    [Fact]
    public async Task RecordingHandler_DefaultConstructor_Returns200AndCapturesLastRequest()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler();
        var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/skills/42");

        var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("/skills/42", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RecordingHandler_WithCustomResponder_UsesResponderAndCapturesLastRequest()
    {
        var responderCalls = 0;
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(_ =>
        {
            responderCalls++;
            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });
        var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

        var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://api.test/skills"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, responderCalls);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public void RecordingHandler_BeforeAnyRequest_HasNullLastRequest()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler();

        Assert.Null(handler.LastRequest);
    }
}