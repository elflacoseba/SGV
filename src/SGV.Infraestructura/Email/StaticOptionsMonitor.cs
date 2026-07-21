using Microsoft.Extensions.Options;

namespace SGV.Infraestructura.Email;

/// <summary>
/// Wraps a single <see cref="IOptions{T}"/> snapshot into an
/// <see cref="IOptionsMonitor{T}"/>. Used when a helper needs the
/// monitor surface but the caller only has an
/// <see cref="IOptions{T}"/> instance.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}