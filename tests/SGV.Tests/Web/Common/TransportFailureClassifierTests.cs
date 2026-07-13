using System.Net.Sockets;
using System.Text.Json;
using SGV.Web.Integration.Common;
using Xunit;

namespace SGV.Tests.Web.Common;

public sealed class TransportFailureClassifierTests
{
    public static TheoryData<Exception> TransportExceptions => new()
    {
        new HttpRequestException("network down"),
        new TaskCanceledException("request timeout"),
        new JsonException("malformed payload")
    };

    [Theory]
    [MemberData(nameof(TransportExceptions))]
    public void IsTransportFailure_KnownTransportExceptions_ReturnsTrue(Exception exception)
    {
        Assert.True(TransportFailureClassifier.IsTransportFailure(exception));
    }

    [Fact]
    public void IsTransportFailure_OperationCanceledWithoutOptIn_ReturnsFalse()
    {
        var exception = new OperationCanceledException("cooperative cancellation");

        Assert.False(TransportFailureClassifier.IsTransportFailure(exception));
    }

    [Fact]
    public void IsTransportFailure_OperationCanceledWithOptIn_ReturnsTrue()
    {
        var exception = new OperationCanceledException("client timeout");

        Assert.True(TransportFailureClassifier.IsTransportFailure(exception, includeOperationCanceled: true));
    }

    // ─────────────────────────────────────────────────────────────────
    // IsDnsFailure (issue #125 / REQ-4): un HttpRequestException cuya
    // InnerException es un SocketException con SocketError.NameResolutionFailure
    // representa una falla de DNS. El helper existe para que los PageModels
    // puedan distinguir DNS de otros HttpRequestException sin parsear el
    // mensaje nativo (que depende de la plataforma / idioma).
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsDnsFailure_NameResolutionFailure_ReturnsTrue()
    {
        // SocketError.HostNotFound = 11001 (en .NET 10 el histórico
        // NameResolutionFailure fue renombrado a HostNotFound). Es la forma
        // más común de DNS-failure observable en HttpRequestException.
        var dnsFailure = new HttpRequestException(
            "No such host is known",
            new SocketException((int)SocketError.HostNotFound));

        Assert.True(TransportFailureClassifier.IsDnsFailure(dnsFailure));
    }

    [Fact]
    public void IsDnsFailure_NonSocketInner_ReturnsFalse()
    {
        var notDns = new HttpRequestException(
            "some other failure",
            new InvalidOperationException("not a socket"));

        Assert.False(TransportFailureClassifier.IsDnsFailure(notDns));
    }

    [Fact]
    public void IsDnsFailure_NullInner_ReturnsFalse()
    {
        var noInner = new HttpRequestException("network down");

        Assert.False(TransportFailureClassifier.IsDnsFailure(noInner));
    }

    [Fact]
    public void IsDnsFailure_SocketInnerWithOtherErrorCode_ReturnsFalse()
    {
        // SocketException con error code distinto (e.g. ConnectionRefused)
        // NO es DNS; debe diferenciarse.
        var otherSocket = new HttpRequestException(
            "connection refused",
            new SocketException((int)SocketError.ConnectionRefused));

        Assert.False(TransportFailureClassifier.IsDnsFailure(otherSocket));
    }

    [Fact]
    public void IsDnsFailure_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => TransportFailureClassifier.IsDnsFailure(null!));
    }
}
