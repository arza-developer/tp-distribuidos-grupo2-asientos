namespace Booking.Api.Repositories
{
    public class ReservationUniqueConflictException: Exception
    {
        public ReservationUniqueConflictException(Exception inner) : base("Se produjo un conflicto de unicidad.", inner)
        {
        }
    }
}
