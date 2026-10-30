using Booking.Api.Repositories;

namespace Booking.Api.Workers;

// [Patron: Hosted Service / BackgroundService] Paso 8 de la consigna (vencimiento). La liberacion de
// asientos se desacopla del flujo HTTP: una limpieza periodica autonoma que no depende de que un usuario
// haga un request. El intervalo se lee de configuracion (ReservationExpiration:IntervalSeconds).
// [Decision] PeriodicTimer en lugar de Task.Delay: mantiene un ritmo fijo aunque un ciclo tarde.
public class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpirationWorker> _logger;
    private readonly TimeSpan _interval;

    public ReservationExpirationWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ReservationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(configuration.GetValue("ReservationExpiration:IntervalSeconds", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Revisando reservas vencidas cada {Interval} segundos", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            // Primer ciclo apenas arranca, despues uno por cada tick del timer.
            do
            {
                await ExpireReservationsAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Apagado normal de la aplicacion
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Un scope por ciclo: DbContext y repositorio nuevos en cada revision
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            var expiredCount = await repository.ExpireOverdueReservationsAsync(DateTime.UtcNow, stoppingToken);
            if (expiredCount > 0)
            {
                _logger.LogInformation("Se vencieron {Count} reservas", expiredCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un ciclo fallido no debe apagar la aplicacion: se reintenta en el proximo tick.
            _logger.LogError(ex, "Error al vencer reservas, se reintenta en el proximo ciclo");
        }
    }
}

