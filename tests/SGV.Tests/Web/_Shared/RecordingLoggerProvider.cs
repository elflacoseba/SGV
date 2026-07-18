using Microsoft.Extensions.Logging;

namespace SGV.Tests.Web._Shared;

/// <summary>
/// Provider de logging in-memory que captura <see cref="LogEntry"/> con su
/// scope estructurado asociado. Pensado para tests de integración web que
/// necesitan assertear tanto el mensaje de log como las propiedades de
/// scope (e.g. <c>Search</c>, <c>Sort</c>, <c>Segmento</c>,
/// <c>CorrelationId</c> cuando el BFF reporta un fallo upstream).
/// </summary>
/// <remarks>
/// <para>
/// Compatible con el patrón canónico de .NET: el <see cref="BeginScope"/>
/// retorna un <see cref="Scope"/> que apila los pares
/// <see cref="KeyValuePair{TKey,TValue}"/> declarados durante el scope
/// y los entrega como un diccionario consolidado a la
/// <see cref="LogEntry.StateDictionary"/>. Esto preserva el contrato
/// observable de <c>ILogger.BeginScope(IReadOnlyCollection&lt;KeyValuePair&lt;string, object&gt;&gt;)</c>
/// que la app usa cuando arma scopes con
/// <c>new Dictionary&lt;string, object&gt; { ... }</c>.
/// </para>
/// <para>
/// Uso típico: agregar el provider vía <c>services.AddLogging(b =&gt; b.AddProvider(...))</c>
/// en el override de servicios del WebApplicationFactory, ejecutar la
/// request y luego inspeccionar <see cref="Entries"/>.
/// </para>
/// </remarks>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _gate = new();

    /// <summary>
    /// Entradas capturadas. El orden refleja la secuencia real del pipeline;
    /// cada entrada lleva su scope consolidado en <see cref="LogEntry.StateDictionary"/>.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private void Append(LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class RecordingLogger(string categoryName, RecordingLoggerProvider owner) : ILogger
    {
        private static readonly AsyncLocal<Scope?> CurrentScope = new();

        private readonly string _categoryName = categoryName;
        private readonly RecordingLoggerProvider _owner = owner;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var scope = new Scope(state, CurrentScope.Value);
            CurrentScope.Value = scope;
            return scope;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scope = CurrentScope.Value;
            var dictionary = scope is null ? null : scope.ToDictionary();
            var message = formatter(state, exception);
            _owner.Append(new LogEntry(_categoryName, logLevel, eventId, message, exception, dictionary));
        }

        private sealed class Scope(object state, Scope? previous) : IDisposable
        {
            private readonly object _state = state;
            private readonly Scope? _previous = previous;

            public IReadOnlyDictionary<string, object?>? ToDictionary()
            {
                var keyValues = ExtractKeyValues(_state);
                if (keyValues is null)
                {
                    return null;
                }

                var flat = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kvp in keyValues)
                {
                    flat[kvp.Key] = kvp.Value;
                }
                return flat;
            }

            public void Dispose() => CurrentScope.Value = _previous;

            private static IReadOnlyCollection<KeyValuePair<string, object?>>? ExtractKeyValues(object state)
            {
                if (state is IReadOnlyCollection<KeyValuePair<string, object?>> typed)
                {
                    return typed;
                }

                if (state is IEnumerable<KeyValuePair<string, object?>> enumerable)
                {
                    return enumerable.ToArray();
                }

                return null;
            }
        }
    }
}

/// <summary>
/// Entrada capturada por <see cref="RecordingLoggerProvider"/>.
/// </summary>
/// <param name="CategoryName">Categoría del logger (e.g. <c>SGV.Web.Personas.BffUpstream</c>).</param>
/// <param name="Level"><see cref="LogLevel"/> efectiva.</param>
/// <param name="EventId"><see cref="EventId"/> reportado.</param>
/// <param name="Message">Mensaje formateado.</param>
/// <param name="Exception">Excepción adjunta, si la hay.</param>
/// <param name="StateDictionary">Scope consolidado al momento del log, o <c>null</c> si no había scope activo.</param>
public sealed record LogEntry(
    string CategoryName,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?>? StateDictionary);