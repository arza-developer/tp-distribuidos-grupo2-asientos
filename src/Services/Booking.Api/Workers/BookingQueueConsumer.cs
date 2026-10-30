using Azure.Messaging.ServiceBus;
using System.Text.Json;
using Booking.Api.Models;
using Booking.Api.Models.Enums;
using Booking.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using SeatsReservation.Contracts.Events;

namespace Booking.Api.Workers;

// [Patron: Hosted Service / BackgroundService] Paso 4 de la consigna. El procesamiento corre en segundo
// plano, desacoplado del flujo HTTP: Availability publica y responde de inmediato (consistencia eventual).
// [Patron: Competing Consumers] Todas las replicas de Booking leen la misma suscripcion; Service Bus
// entrega cada mensaje a una sola de ellas.
public class BookingQueueConsumer : BackgroundService
{
    private static readonly TimeSpan RetentionTime = TimeSpan.FromMinutes(5);
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

        // [Patron: Unit of Work por mensaje] El worker es Singleton y el DbContext es Scoped: un scope por
        // mensaje da DbContext y repositorio nuevos, igual que un request HTTP.
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        // [Patron: Idempotent Consumer] Service Bus entrega "al menos una vez": si el mensaje llega repetido
        // (mismo ReservationId guardado como RequestId), ya fue procesado y se descarta.
        if (await repository.GetByRequestIdAsync(evt.ReservationId) is not null)
        {
            _logger.LogInformation("Reserva {ReservationId} ya procesada, se descarta", evt.ReservationId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        var seatTaken = await repository.HasActiveReservationAsync(evt.EventId, evt.SeatNumber);
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            RequestId = evt.ReservationId,
            EventId = evt.EventId,
            SeatNumber = evt.SeatNumber,
            UserId = evt.UserId.ToString(),
            CreatedAtUtc = now,
            Status = seatTaken ? ReservationStatus.Rejected : ReservationStatus.Retained,
            ExpiresAtUtc = seatTaken ? null : now.Add(RetentionTime),
            RejectionReason = seatTaken ? ReservationRejectionReason.SeatOccupied : null
        };

        try
        {
            await repository.AddReservationAsync(reservation, args.CancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // [Patron: Concurrencia optimista con indice unico] Otra replica gano la carrera y el indice
            // unico (EventId, SeatNumber) freno el insert. Se abandona el mensaje para reintentarlo:
            // en el proximo intento cae en idempotencia o queda Rejected por asiento ocupado.
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
