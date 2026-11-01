using Availability.Application.Abstractions;
using MediatR;

namespace Availability.Application.Seats.Queries.GetSeats;

public sealed class GetSeatsQueryHandler(ISeatRepository seats)
    : IRequestHandler<GetSeatsQuery, IReadOnlyList<SeatDto>>
{
    public async Task<IReadOnlyList<SeatDto>> Handle(GetSeatsQuery request, CancellationToken ct)
    {
        var result = await seats.ListByEventAsync(request.EventId, ct);

        return result
            .OrderBy(s => s.SeatNumber, StringComparer.Ordinal)
            .Select(s => new SeatDto(s.SeatNumber, s.Price, s.IsAvailable))
            .ToList();
    }
}
