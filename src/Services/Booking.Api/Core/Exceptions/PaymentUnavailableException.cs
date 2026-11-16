namespace Booking.Api.Core.Exceptions
{
    public class PaymentUnavailableException: Exception
    {
        public PaymentUnavailableException(string message) : base(message)
        {
        }
    }
}
