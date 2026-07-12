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
    public SgvWebApplicationFactory Factory => factory;

    public HttpClient Client => client;

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        sentinel.Dispose();
        await factory.DisposeAsync();
    }
}