using Availability.Application.Abstractions;
using Availability.Application.Behaviors;
using Availability.Application.Exceptions;
using Availability.Domain.Exceptions;
using Availability.Tests.Support;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Availability.Tests.Unit;

public class LoggingBehaviorTests
{
    private sealed record SampleRequest(Guid Id, string Name) : IRequest<string>;

    private sealed class FakeCorrelation(string id) : ICorrelationIdAccessor
    {
        public string CorrelationId { get; } = id;
    }

    /// <summary>Logger a mano: guarda nivel, mensaje y scopes activos en cada entrada.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public sealed record Entry(LogLevel Level, string Message, Exception? Exception, IReadOnlyList<object?> Scopes,
            IReadOnlyDictionary<string, object?> State);

        private readonly LoggerExternalScopeProvider _scopes = new();
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<object?>();
            _scopes.ForEachScope((s, l) => l.Add(s), scopes);
            var props = (state as IEnumerable<KeyValuePair<string, object?>>)?.ToDictionary(p => p.Key, p => p.Value)
                        ?? new Dictionary<string, object?>();
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception, scopes, props));
        }
    }

    private readonly CapturingLogger<LoggingBehavior<SampleRequest, string>> _logger = new();
    private readonly FakeTimeProvider _clock = new();

    private LoggingBehavior<SampleRequest, string> Create(string correlationId = "corr-77") =>
        new(_logger, new FakeCorrelation(correlationId), _clock);

    private static SampleRequest Req() => new(Guid.NewGuid(), "x");

    [Fact]
    public async Task Handle_Success_LogsInicioThenFin_AndReturnsTheResponse()
    {
        var request = Req();

        var response = await Create().Handle(request, () => Task.FromResult("ok"), default);

        Assert.Equal("ok", response);
        Assert.Equal(2, _logger.Entries.Count);
        Assert.StartsWith("INICIO: SampleRequest", _logger.Entries[0].Message);
        Assert.StartsWith("FIN: SampleRequest", _logger.Entries[1].Message);
        Assert.All(_logger.Entries, e => Assert.Equal(LogLevel.Information, e.Level));
    }

    [Fact]
    public async Task Handle_InicioIncludesTheRequestData()
    {
        var request = Req();

        await Create().Handle(request, () => Task.FromResult("ok"), default);

        Assert.Contains(request.Id.ToString(), _logger.Entries[0].Message);
    }

    [Fact]
    public async Task Handle_EveryEntryCarriesTheCorrelationIdInScope()
    {
        await Create("corr-abc").Handle(Req(), () => Task.FromResult("ok"), default);

        Assert.All(_logger.Entries, e =>
        {
            var scope = Assert.Single(e.Scopes);
            var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope);
            Assert.Equal("corr-abc", dict["CorrelationId"]);
        });
    }

    [Fact]
    public async Task Handle_FinReportsTheElapsedTime()
    {
        await Create().Handle(Req(), () =>
        {
            _clock.Advance(TimeSpan.FromMilliseconds(250));
            return Task.FromResult("ok");
        }, default);

        Assert.Equal(250d, (double)_logger.Entries[1].State["ElapsedMs"]!, 1);
        Assert.Contains("250 ms", _logger.Entries[1].Message);
    }

    [Fact]
    public async Task Handle_BusinessError_LogsFinAsInformationWithCode_AndRethrows()
    {
        var ex = await Assert.ThrowsAsync<SeatNotFoundException>(() =>
            Create().Handle(Req(), () => throw new SeatNotFoundException(Guid.NewGuid(), "Z-9"), default));

        var fin = _logger.Entries[1];
        Assert.Equal(LogLevel.Information, fin.Level);
        Assert.Contains("seat.not_found", fin.Message);
        Assert.Contains("error de negocio", fin.Message);
        Assert.Null(fin.Exception); // la traza la registra el manejador de excepciones
        Assert.Equal("seat.not_found", ex.Code);
    }

    [Theory]
    [InlineData(typeof(MessagePublishException))]
    [InlineData(typeof(BookingUnavailableException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task Handle_NonBusinessError_LogsFinAsError_WithTypeName_AndRethrows(Type exceptionType)
    {
        var thrown = (Exception)(exceptionType == typeof(InvalidOperationException)
            ? new InvalidOperationException("boom")
            : Activator.CreateInstance(exceptionType, "boom", null)!);

        await Assert.ThrowsAsync(exceptionType, () => Create().Handle(Req(), () => throw thrown, default));

        var fin = _logger.Entries[1];
        Assert.Equal(LogLevel.Error, fin.Level);
        Assert.Contains(exceptionType.Name, fin.Message);
        Assert.Null(fin.Exception);
    }

    [Fact]
    public async Task Handle_Failure_StillLogsInicioFirst()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create().Handle(Req(), () => throw new InvalidOperationException(), default));

        Assert.StartsWith("INICIO:", _logger.Entries[0].Message);
        Assert.StartsWith("FIN:", _logger.Entries[1].Message);
        Assert.Equal(2, _logger.Entries.Count);
    }
}
