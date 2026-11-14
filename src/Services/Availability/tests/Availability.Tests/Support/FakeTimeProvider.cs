namespace Availability.Tests.Support;

/// <summary>
/// Reloj escrito a mano: arranca en la hora real y avanza solo cuando el test lo pide.
/// Controla tanto la hora (<see cref="GetUtcNow"/>) como los timestamps de medición de duración
/// (<see cref="GetTimestamp"/>/<see cref="TimeProvider.GetElapsedTime(long)"/>), para probar tiempos de forma determinista.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _now.UtcTicks;

    public void Advance(TimeSpan by) => _now += by;
}
