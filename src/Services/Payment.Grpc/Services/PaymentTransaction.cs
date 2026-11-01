namespace Payment.Grpc.Services;

public class PaymentTransaction
{
    public int Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Success, Failed, etc.
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}