using System.Text.Json;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Classifies exceptions that represent recoverable transport or payload failures
/// from typed HTTP clients.
/// </summary>
public static class TransportFailureClassifier
{
    /// <summary>
    /// Returns <see langword="true"/> when an exception can be presented to the
    /// user as a recoverable upstream service failure.
    /// </summary>
    public static bool IsTransportFailure(Exception exception, bool includeOperationCanceled = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is HttpRequestException
            || exception is TaskCanceledException
            || exception is JsonException
            || (includeOperationCanceled && exception is OperationCanceledException);
    }
}
