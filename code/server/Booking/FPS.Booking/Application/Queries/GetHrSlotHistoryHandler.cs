using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetHrSlotHistoryHandler : IRequestHandler<GetHrSlotHistoryQuery, HrSlotHistoryResult>
{
    private readonly IBookingQueryRepository queryRepository;

    public GetHrSlotHistoryHandler(IBookingQueryRepository queryRepository)
    {
        ArgumentNullException.ThrowIfNull(queryRepository);
        this.queryRepository = queryRepository;
    }

    public Task<HrSlotHistoryResult> Handle(GetHrSlotHistoryQuery query, CancellationToken cancellationToken)
        => queryRepository.GetSlotHistoryAsync(
            query.TenantId,
            query.LocationId,
            query.SlotId,
            query.From,
            query.To,
            query.PageSize,
            query.Cursor,
            cancellationToken);
}
