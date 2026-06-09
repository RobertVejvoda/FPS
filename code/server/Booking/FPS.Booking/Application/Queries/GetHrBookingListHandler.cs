using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetHrBookingListHandler : IRequestHandler<GetHrBookingListQuery, HrBookingListResult>
{
    private readonly IBookingQueryRepository queryRepository;

    public GetHrBookingListHandler(IBookingQueryRepository queryRepository)
    {
        ArgumentNullException.ThrowIfNull(queryRepository);
        this.queryRepository = queryRepository;
    }

    public Task<HrBookingListResult> Handle(GetHrBookingListQuery query, CancellationToken cancellationToken)
        => queryRepository.GetByTenantAsync(
            query.TenantId,
            query.LocationId,
            query.From,
            query.To,
            query.StatusFilter,
            query.PageSize,
            query.Cursor,
            cancellationToken);
}
