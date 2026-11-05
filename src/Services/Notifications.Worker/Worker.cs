
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using SeatsReservation.Contracts.Events;
using Serilog.Context;

namespace Notifications.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusWorkerOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Worker(
        ILogger<Worker> logger,
        ServiceBusClient serviceBusClient,
        ServiceBusWorkerOptions options)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _options = options;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Notifications Worker iniciado. Topic: {TopicName}. Suscripción: {SubscriptionName}",
            _options.TopicName,
            _options.SubscriptionName);

        await using ServiceBusProcessor processor =
            _serviceBusClient.CreateProcessor(
                _options.TopicName,
                _options.SubscriptionName,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 1
                });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        try
        {
            await processor.StartProcessingAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Cierre normal del servicio.
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None);
        }

        _logger.LogInformation("Notifications Worker detenido.");
    }

    private async Task ProcessMessageAsync(
        ProcessMessageEventArgs args)
    {
        string messageId = args.Message.MessageId;

        try
        {
            ReservationConfirmedEvent? reservation =
                JsonSerializer.Deserialize<ReservationConfirmedEvent>(
                    args.Message.Body.ToString(),
                    JsonOptions);

            if (reservation is null ||
                reservation.ReservationId == Guid.Empty ||
                reservation.UserId == Guid.Empty ||
                string.IsNullOrWhiteSpace(reservation.SeatNumber))
            {
                _logger.LogWarning(
                    "Mensaje inválido recibido. MessageId: {MessageId}",
                    messageId);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "InvalidReservationEvent",
                    "El mensaje no contiene los datos requeridos.");

                return;
            }

            string correlationId =
                !string.IsNullOrWhiteSpace(reservation.CorrelationId)
                    ? reservation.CorrelationId
                    : args.Message.CorrelationId ?? "No disponible";

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("ReservationId", reservation.ReservationId))
            using (LogContext.PushProperty("UserId", reservation.UserId))
            {
                _logger.LogInformation(
                    "[NOTIFICACIÓN] Reserva {ReservationId} confirmada para el usuario {UserId}. Asiento: {SeatNumber}. Transacción: {TransactionId}. CorrelationId: {CorrelationId}",
                    reservation.ReservationId,
                    reservation.UserId,
                    reservation.SeatNumber,
                    reservation.TransactionId,
                    correlationId);

                _logger.LogInformation(
                    "[NOTIFICACIÓN SIMULADA] Email/push enviado al usuario {UserId} por la reserva {ReservationId}. CorrelationId: {CorrelationId}",
                    reservation.UserId,
                    reservation.ReservationId,
                    correlationId);
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "No se pudo deserializar el mensaje {MessageId}",
                messageId);

            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidJson",
                "El cuerpo del mensaje no contiene JSON válido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al procesar el mensaje {MessageId}. Se intentará nuevamente si corresponde.",
                messageId);

            throw;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Error de Azure Service Bus. Entidad: {EntityPath}. Origen: {ErrorSource}",
            args.EntityPath,
            args.ErrorSource);

        return Task.CompletedTask;
    }
}
