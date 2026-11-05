using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // <-- 1. Agregar este using
using Payment.Grpc.DataAccess;
using Payment.Grpc.Domain;
using SeatsReservation.Contracts.Grpc;

namespace Payment.Grpc.Business;

[Authorize]
public class PaymentService : PaymentApi.PaymentApiBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<PaymentService> _logger;
    private readonly IConfiguration _configuration; // <-- 2. Declarar la variable

    public PaymentService(PaymentDbContext dbContext, ILogger<PaymentService> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration; // <-- 3. Inyectarla en el constructor
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

        // 2. Leer el switch de simulación de fallo (Pruebas de caos / Circuit Breaker)
        bool simulateFailure = _configuration.GetValue<bool>("PaymentSettings:SimulateFailure");
        bool success = !simulateFailure; // Si está en true, forzamos el fallo

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
            RejectionReason = success ? string.Empty : "Fallo simulado por configuración (Chaos Testing)."
        };
    }
}