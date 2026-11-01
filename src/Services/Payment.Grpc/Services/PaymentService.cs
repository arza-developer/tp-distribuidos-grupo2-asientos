using Grpc.Core;
using Microsoft.Extensions.Configuration;
using SeatsReservation.Contracts.Grpc;

public class PaymentService : PaymentApi.PaymentApiBase
{
    private readonly IConfiguration _configuration;
    private readonly PaymentDbContext _dbContext;

    public PaymentService(IConfiguration configuration, PaymentDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public override async Task<PaymentResponse> ProcessPayment(PaymentRequest request, ServerCallContext context)
    {
        bool simulateFailure = _configuration.GetValue<bool>("PaymentSettings:SimulateFailure");
        bool isSuccess = !simulateFailure && request.Amount > 0;
        string transactionId = Guid.NewGuid().ToString(); // Inmutable

        var transaction = new PaymentTransaction
        {
            TransactionId = transactionId,
            ReservationId = request.ReservationId,
            UserId = request.UserId,
            Amount = request.Amount,
            IsSuccess = isSuccess,
            RejectionReason = isSuccess ? string.Empty : (simulateFailure ? "Fallo simulado por Circuit Breaker" : "Fondos insuficientes")
        };

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        return new PaymentResponse
        {
            IsSuccess = isSuccess,
            TransactionId = isSuccess ? transactionId : string.Empty,
            RejectionReason = transaction.RejectionReason
        };
    }
}