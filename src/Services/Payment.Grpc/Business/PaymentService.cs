using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Payment.Grpc.DataAccess;
using Payment.Grpc.Domain; 
using SeatsReservation.Contracts.Grpc;

namespace Payment.Grpc.Business;

[Authorize]
public class PaymentService : PaymentApi.PaymentApiBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentDbContext dbContext, ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<PaymentResponse> ProcessPayment(PaymentRequest request, ServerCallContext context)
    {
        var correlationId = request.CorrelationId;

        if (string.IsNullOrEmpty(correlationId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "El CorrelationId es obligatorio para garantizar la idempotencia."));
        }

        // 1. Control de Idempotencia: Verificar si ya fue procesado
        var existingTransaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.CorrelationId == correlationId);

        if (existingTransaction != null)
        {
            _logger.LogInformation("Pago duplicado detectado por retry. CorrelationId: {CorrelationId} ya procesado.", correlationId);
            return new PaymentResponse
            {
                IsSuccess = existingTransaction.Status == "Success",
                TransactionId = existingTransaction.Id.ToString(),
                RejectionReason = existingTransaction.Status == "Success" ? string.Empty : "Transacción ya procesada anteriormente (Idempotente)."
            };
        }

        // 2. Procesamiento normal del pago (o simulación)
        bool success = true;

        // 3. Guardar en SQLite
        var transaction = new PaymentTransaction
        {
            CorrelationId = correlationId,
            Amount = (decimal)request.Amount,
            Status = success ? "Success" : "Failed",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        return new PaymentResponse
        {
            IsSuccess = success,
            TransactionId = transaction.Id.ToString(),
            RejectionReason = success ? string.Empty : "El pago falló."
        };
    }
}