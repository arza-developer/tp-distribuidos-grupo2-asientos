namespace Booking.Api.Core.Exceptions
{
    public class ReservationUniqueConflictException: Exception
    {
        public ReservationUniqueConflictException(Exception inner) : base("Se produjo un conflicto de unicidad.", inner)
        {
        }
    }
}
