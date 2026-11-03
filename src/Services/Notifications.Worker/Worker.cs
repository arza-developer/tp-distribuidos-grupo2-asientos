using Serilog;
using Serilog.Context;

namespace Notifications.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Simulación: Aquí extraerías el CorrelationId del mensaje de la cola de Azurite
            // Usando por ejemplo: MessageCorrelationExtensions.ExtractCorrelationId(properties)
            string correlationId = Guid.NewGuid().ToString();

            // Inyectamos el CorrelationId en el contexto de log de este bloque
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                Log.Information("INICIO: Procesando notificación en segundo plano.");

                try
                {
                    // Lógica para enviar la notificación (Email, Push, etc.)
                    await Task.Delay(1000, stoppingToken);

                    Log.Information("FIN: Notificación procesada y enviada exitosamente.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "FIN (Error): Falló el envío de la notificación.");
                }
            }

            // Espera antes del próximo ciclo de lectura de la cola
            await Task.Delay(5000, stoppingToken);
        }
    }
}