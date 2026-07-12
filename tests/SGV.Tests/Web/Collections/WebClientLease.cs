using System.Net.Http;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Lease que retiene un <see cref="HttpClient"/> autenticado contra
/// <see cref="SgvWebApplicationFactory"/> y libera los tres recursos en el
/// orden <c>client → sentinel → factory</c> al hacer <see cref="DisposeAsync"/>.
/// El orden es crítico: si la factory (host) se detuviera antes que el
/// cliente, el socket quedaría colgado (design.md §"Riesgos").
/// </summary>
public sealed class WebClientLease(SgvWebApplicationFactory factory, HttpClient client, TestSentinel sentinel) : IAsyncDisposable
{
    private int _disposed;

    public SgvWebApplicationFactory Factory => factory;

    public HttpClient Client => client;

    public async ValueTask DisposeAsync()
    {
        // Idempotencia: una segunda llamada (p. ej. lease dentro de un
        // `await using` que también se dispone explícitamente, o doble
        // dispose manual) NO debe volver a cerrar el cliente, decrementar
        // el sentinel, ni detener la factory. De lo contrario,
        // `TestSentinel.AliveCount` baja dos veces y contamina el estado
        // compartido de los demás tests de la colección `WebIntegration`.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        client.Dispose();
        sentinel.Dispose();
        await factory.DisposeAsync();
    }
}