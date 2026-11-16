using Azure.Messaging.ServiceBus;
using Booking.Api.Business.DTOs;
using Booking.Api.DataAccess.Models;
using Booking.Api.Business.Services;
using SeatsReservation.Contracts.Events;
using System.Text.Json;
using Booking.Api.Core.Exceptions;

namespace Booking.Api.Presentation.Workers;

// [Patron: Hosted Service / BackgroundService] Paso 4 de la consigna. El procesamiento corre en segundo
// plano, desacoplado del flujo HTTP: Availability publica y responde de inmediato (consistencia eventual).
// [Patron: Competing Consumers] Todas las replicas de Booking leen la misma suscripcion; Service Bus
// entrega cada mensaje a una sola de ellas.
public class BookingQueueConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingQueueConsumer> _logger;

    public BookingQueueConsumer(ServiceBusClient client, IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<BookingQueueConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _processor = client.CreateProcessor(
            configuration["ServiceBus:TopicName"],
            configuration["ServiceBus:SubscriptionName"],
            // [Patron: Peek-Lock + confirmacion manual] El mensaje solo se borra cuando lo completamos,
            // despues de guardar en la base. Si el proceso cae antes, Service Bus lo vuelve a entregar.
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
        ReservationRequestedEvent? evt;
        try
        {
            evt = args.Message.Body.ToObjectFromJson<ReservationRequestedEvent>(JsonOptions);
        }
        catch (JsonException ex)
        {
            evt = null;
            _logger.LogWarning(ex, "Mensaje {MessageId} con JSON invalido", args.Message.MessageId);
        }

        if (evt is null)
        {
            // [Patron: Dead Letter Channel] Mensaje veneno: reintentarlo no sirve, va directo a dead-letter
            // para no bloquear la suscripcion y poder inspeccionarlo despues.
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "No se pudo leer ReservationRequestedEvent.");
            return;
        }

        // [Patron: Unit of Work por mensaje] El worker es Singleton y el Service (con su DbContext) es Scoped:
        // un scope por mensaje da instancias nuevas, igual que un request HTTP.
        using var scope = _scopeFactory.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var request = new ReserveSeatRequest
        {
            RequestId = evt.ReservationId,
            EventId = evt.EventId,
            SeatNumber = evt.SeatNumber,
            UserId = evt.UserId.ToString()
        };

        Reservation reservation;
        try
        {
            // [Patron: Idempotent Consumer] El service descarta los mensajes repetidos (mismo RequestId)
            // y devuelve la reserva que existe
            reservation = await bookingService.ProcessBookingAsync(request, evt.Price, args.CancellationToken);
        }
        catch (PaymentUnavailableException ex)
        {
            // [Patron: Competing Consumers] El service no pudo procesar la reserva por un error de pago
            // (por ejemplo, el servicio de pagos no esta disponible). Se abandona el mensaje para reintentarlo.
            _logger.LogWarning(ex, "Reserva {ReservationId} no pudo procesarse por error de pago, se reintenta", evt.ReservationId);
            await args.AbandonMessageAsync(args.Message);
            return;
        }
        catch (ArgumentException ex)
        {
            // [Patron: Dead Letter Channel] Datos invalidos: reintentar no los arregla.
            _logger.LogWarning(ex, "Reserva {ReservationId} con datos invalidos", evt.ReservationId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidData", ex.Message);
            return;
        }
        catch (ReservationUniqueConflictException ex)
        {
            // Conflicto que el service no pudo resolver: se abandona el mensaje para reintentarlo.
            _logger.LogWarning(ex, "Conflicto al guardar la reserva {ReservationId}, se reintenta", evt.ReservationId);
            await args.AbandonMessageAsync(args.Message);
            return;
        }

        _logger.LogInformation("Reserva {ReservationId} registrada como {Status}", reservation.Id, reservation.Status);
        await args.CompleteMessageAsync(args.Message);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error de Service Bus en {EntityPath}", args.EntityPath);
        return Task.CompletedTask;
    }
}
