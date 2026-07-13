using System.Net.Sockets;
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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="exception"/> wraps a
    /// DNS resolution failure. The check is by
    /// <see cref="SocketException.SocketErrorCode"/> (target-framework-native:
    /// <c>SocketError.HostNotFound</c> en .NET 10, antes
    /// <c>SocketError.NameResolutionFailure</c>) — no se parsea el mensaje de
    /// la excepción porque su texto depende de plataforma e idioma.
    /// </summary>
    /// <remarks>
    /// Issue #125 / REQ-4: una falla DNS no debe convertirse en
    /// <c>ErrorCategoria.Transport</c> desde el cliente HTTP; la excepción se
    /// propaga nativa y este helper permite que el <c>PageModel</c> la
    /// distinga de otros <see cref="HttpRequestException"/> (e.g. timeout de
    /// TCP) sin importar el idioma del mensaje.
    /// </remarks>
    public static bool IsDnsFailure(HttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is SocketException se
            && (se.SocketErrorCode == SocketError.HostNotFound
                || se.SocketErrorCode == SocketError.TryAgain
                || se.SocketErrorCode == SocketError.NoRecovery
                || se.SocketErrorCode == SocketError.NoData);
    }
}
