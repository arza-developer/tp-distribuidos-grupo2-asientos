public class PaymentTransaction
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string ReservationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public double Amount { get; set; }
    public bool IsSuccess { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}