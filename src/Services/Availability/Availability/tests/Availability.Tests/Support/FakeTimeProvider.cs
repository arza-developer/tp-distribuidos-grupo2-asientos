namespace Availability.Tests.Support;

/// <summary>Reloj escrito a mano: arranca en la hora real y avanza solo cuando el test lo pide.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
