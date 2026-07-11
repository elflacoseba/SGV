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
}
