using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Availability.Tests.Support;

/// <summary>Sink de Serilog que guarda los eventos en memoria (se registra por DI en la fábrica de API).</summary>
public sealed class CapturingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>Eventos que llevan el CorrelationId dado (cada test usa el suyo para no mezclarse con otros).</summary>
    public IReadOnlyList<LogEvent> For(string correlationId) =>
        Events.Where(e => e.Properties.TryGetValue("CorrelationId", out var v)
                          && v is ScalarValue { Value: string s } && s == correlationId).ToList();
}
