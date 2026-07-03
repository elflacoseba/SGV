using System.Net;

namespace SGV.Tests.Web._Shared;

/// <summary>
/// Helper compartido para tests de fallos de transporte y cancelación
/// cooperativa en los clientes HTTP tipados de <c>SGV.Web</c>.
/// </summary>
/// <remarks>
/// Solo fabrica <see cref="HttpMessageHandler"/>s y datasets: nunca ejecuta
/// lógica de los clientes bajo prueba. Si el contrato de propagación cambia,
/// fallan los tests que llaman a <c>HabilidadApiClient.QueryAsync</c> o
/// <c>CargoApiClient.QueryAsync</c>, no el helper.
/// </remarks>
public static class HttpClientExceptionScenarios
{
    /// <summary>
    /// Filas parametrizadas para <c>[Theory]</c> con <c>[MemberData]</c>.
    /// Cada fila es <c>[string scenario, Func&lt;Exception&gt; factory, Type expectedExceptionType]</c>.
    /// </summary>
    public static IEnumerable<object[]> TransportExceptionData =>
    [
        ["TaskCanceled", () => new TaskCanceledException("Simulated timeout"), typeof(TaskCanceledException)],
        ["HttpRequest", () => new HttpRequestException("Simulated transport failure"), typeof(HttpRequestException)]
    ];

    /// <summary>
    /// Construye un <see cref="HttpMessageHandler"/> cuyo <c>SendAsync</c>
    /// invoca la factory y propaga la excepción resultante al pipeline HTTP.
    /// </summary>
    public static HttpMessageHandler NewHandlerThrowing(Func<Exception> exceptionFactory) =>
        new ThrowingHandler(exceptionFactory);

    /// <summary>
    /// Construye un <see cref="RecordingHandler"/> con respuesta por defecto
    /// (<c>200 OK</c>) y captura de <see cref="HttpRequestMessage"/>.
    /// </summary>
    public static RecordingHandler NewRecordingHandler() =>
        new();

    /// <summary>
    /// Construye un <see cref="RecordingHandler"/> que delega la respuesta al
    /// <paramref name="responder"/> provisto. Útil para reemplazar handlers
    /// stub existentes que necesitan lógica de respuesta custom.
    /// </summary>
    public static RecordingHandler NewRecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(responder);

    /// <summary>
    /// Handler que captura la última <see cref="HttpRequestMessage"/> recibida
    /// y delega la construcción de la respuesta. Pensado para que los tests
    /// inspeccionen rutas, métodos y query strings sin re-implementar el seam.
    /// </summary>
    public sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        /// <summary>
        /// Crea un handler con respuesta por defecto <c>200 OK</c>.
        /// </summary>
        public RecordingHandler()
            : this(_ => new HttpResponseMessage(HttpStatusCode.OK))
        {
        }

        /// <summary>
        /// Crea un handler cuya respuesta se calcula vía
        /// <paramref name="responder"/> por cada solicitud recibida.
        /// </summary>
        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        /// <summary>
        /// Última solicitud recibida por el handler, o <c>null</c> si aún no
        /// se invocó <c>SendAsync</c> o si el token estaba cancelado.
        /// </summary>
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Respeta el contrato de cancelación antes de capturar la solicitud,
            // para reflejar el comportamiento de los handlers reales y permitir
            // que los tests de token pre-cancelado afirmen LastRequest == null.
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class ThrowingHandler(Func<Exception> exceptionFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exceptionFactory();
    }
}