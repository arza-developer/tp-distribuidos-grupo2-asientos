using Azure.Messaging.ServiceBus;

namespace Booking.Api.Workers;

    public class BookingQueueConsumer : BackgroundService
    {
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger<BookingQueueConsumer> _logger;
        
        public BookingQueueConsumer(ServiceBusClient client, IConfiguration configuration, ILogger<BookingQueueConsumer> logger)
        {
            _logger = logger;
            _processor = client.CreateProcessor(
                configuration["ServiceBus:TopicName"],
                configuration["ServiceBus:SubscriptionName"],
                new ServiceBusProcessorOptions { AutoCompleteMessages = false }
                );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _processor.ProcessMessageAsync += HandleMessageAsync;
            _processor.ProcessErrorAsync += HandleErrorAsync;

            await _processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Escuchando el topic de reservas...");

            try
            {
                // Mantiene vivo el worker hasta que la aplicacion se apague.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Apagado normal de la aplicacion.
            }

            await _processor.StopProcessingAsync();
            await _processor.DisposeAsync();
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args)
        {
            _logger.LogInformation("Mensaje recibido: {Body}", args.Message.Body.ToString());
            await args.CompleteMessageAsync(args.Message);
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
        _logger.LogError(args.Exception, "Error de Service Bus en {EntityPath}", args.EntityPath);
        return Task.CompletedTask;
        }
    }
