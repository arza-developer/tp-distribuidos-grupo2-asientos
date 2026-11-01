using Availability.Application.Exceptions;
using Availability.Domain.Exceptions;

namespace Availability.Tests.Unit;

public class BusinessExceptionTests
{
    [Fact]
    public void SeatNotFound_IsNotFoundCategory_WithStableCodeAndContextInMessage()
    {
        var eventId = Guid.NewGuid();

        var ex = new SeatNotFoundException(eventId, "Z-9");

        Assert.IsAssignableFrom<BusinessNotFoundException>(ex);
        Assert.IsAssignableFrom<BusinessException>(ex);
        Assert.Equal("seat.not_found", ex.Code);
        Assert.Contains("Z-9", ex.Message);
        Assert.Contains(eventId.ToString(), ex.Message);
    }

    [Fact]
    public void InvalidReservationRequest_IsValidationCategory_WithReasonInMessage()
    {
        var ex = new InvalidReservationRequestException("UserId vacío");

        Assert.IsAssignableFrom<BusinessValidationException>(ex);
        Assert.IsAssignableFrom<BusinessException>(ex);
        Assert.Equal("reservation.invalid_request", ex.Code);
        Assert.Contains("UserId vacío", ex.Message);
    }

    [Fact]
    public void SeatUnavailable_IsConflictCategory_NotNotFound_WithCleanMessage()
    {
        var eventId = Guid.NewGuid();

        var ex = new SeatUnavailableException(eventId, "A-1");

        Assert.IsAssignableFrom<BusinessConflictException>(ex);
        Assert.IsNotAssignableFrom<BusinessNotFoundException>(ex);
        Assert.Equal("seat.unavailable", ex.Code);
        Assert.Equal($"El asiento 'A-1' del evento '{eventId}' no está disponible.", ex.Message);
        Assert.DoesNotContain("no existe", ex.Message);
    }

    [Fact]
    public void ReservationUserMismatch_IsForbiddenCategory_WithStableCode()
    {
        var ex = new ReservationUserMismatchException();

        Assert.IsAssignableFrom<BusinessForbiddenException>(ex);
        Assert.IsAssignableFrom<BusinessException>(ex);
        Assert.IsNotAssignableFrom<BusinessNotFoundException>(ex);
        Assert.Equal("reservation.user_mismatch", ex.Code);
    }

    [Fact]
    public void Categories_AreDistinct()
    {
        Assert.False(typeof(BusinessNotFoundException).IsAssignableFrom(typeof(InvalidReservationRequestException)));
        Assert.False(typeof(BusinessValidationException).IsAssignableFrom(typeof(SeatNotFoundException)));
        Assert.False(typeof(BusinessConflictException).IsAssignableFrom(typeof(SeatNotFoundException)));
        Assert.False(typeof(BusinessConflictException).IsAssignableFrom(typeof(InvalidReservationRequestException)));
    }

    [Fact]
    public void ReservationNotFound_IsNotFoundCategory_WithStableCodeAndIdInMessage()
    {
        var id = Guid.NewGuid();

        var ex = new ReservationNotFoundException(id);

        Assert.IsAssignableFrom<BusinessNotFoundException>(ex);
        Assert.Equal("reservation.not_found", ex.Code);
        Assert.Contains(id.ToString(), ex.Message);
    }

    [Fact]
    public void BookingUnavailable_IsTechnical_NotBusiness_AndKeepsInnerException()
    {
        var inner = new HttpRequestException("sin red");

        var ex = new BookingUnavailableException("Booking caído", inner);

        Assert.IsNotAssignableFrom<BusinessException>(ex);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void MessagePublish_IsTechnical_NotBusiness_AndKeepsInnerException()
    {
        var inner = new InvalidOperationException("boom");

        var ex = new MessagePublishException("broker caído", inner);

        Assert.IsNotAssignableFrom<BusinessException>(ex);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Domain_HasNoProjectOrPackageDependencies()
    {
        var refs = typeof(BusinessException).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(refs, n => n.StartsWith("Availability.", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.", StringComparison.Ordinal));
    }
}
