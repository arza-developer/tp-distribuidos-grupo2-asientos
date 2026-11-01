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
    public void Categories_AreDistinct()
    {
        Assert.False(typeof(BusinessNotFoundException).IsAssignableFrom(typeof(InvalidReservationRequestException)));
        Assert.False(typeof(BusinessValidationException).IsAssignableFrom(typeof(SeatNotFoundException)));
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
