using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SGV.Tests.Api.Collections;

/// <summary>
/// Fixture raíz de la suite de integración de API. Posee una única
/// <see cref="ApiWebApplicationFactory"/> base con la configuración por
/// defecto (fakes estándar) y la expone como <see cref="RootFactory"/> para
/// que los tests que NO requieren overrides compartan el mismo host sin
/// crear factories adicionales.
///
/// Los tests que necesitan overrides de servicios o configuración deben
/// derivar una factory vía <see cref="ApiWebApplicationFactory.WithOverrides"/>
/// (típicamente <c>_fixture.RootFactory.WithOverrides(...)</c>). La factory
/// derivada es <see cref="IAsyncDisposable"/> y pertenece al test que la pide;
/// el fixture sólo libera la raíz al cierre de la colección.
/// </summary>
public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _root;
    private int _disposed;

    public ApiIntegrationFixture() => _root = new ApiWebApplicationFactory();

    /// <summary>Acceso a la factory raíz compartida (fakes estándar).</summary>
    public ApiWebApplicationFactory RootFactory => _root;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Idempotencia: una segunda llamada NO debe volver a disponer la root.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _root.DisposeAsync();
    }
}