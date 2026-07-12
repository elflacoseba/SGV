namespace SGV.Tests.Web.Collections;

/// <summary>
/// Sentinel observable que cuenta cuántas instancias vivas existen en el
/// proceso de test. El lease de SGV.Tests lo retiene durante su vida útil y
/// lo libera vía <see cref="Dispose"/> como paso intermedio entre cerrar el
/// <see cref="System.Net.Http.HttpClient"/> y detener el host de
/// <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Diseño: design.md §"Firmas explícitas del composite".
/// </summary>
public sealed class TestSentinel : IDisposable
{
    private static int _alive;

    private int _disposed;

    public static int AliveCount => Volatile.Read(ref _alive);

    public TestSentinel() => Interlocked.Increment(ref _alive);

    /// <summary>True tras <see cref="Dispose"/>. Útil para tests bajo paralelismo xUnit.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        // Idempotencia: una segunda llamada (p. ej. lease dispuesto dentro de
        // un `await using` y luego explícitamente, o dos llamadas manuales)
        // NO debe volver a decrementar el contador global compartido.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Decrement(ref _alive);
    }
}