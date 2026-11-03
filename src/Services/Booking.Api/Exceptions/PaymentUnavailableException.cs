namespace Booking.Api.Exceptions
{
    public class PaymentUnavailableException: Exception
    {
        public PaymentUnavailableException(string message) : base(message)
        {
        }
    }
}
