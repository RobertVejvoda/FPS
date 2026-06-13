using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetHrEmployeeHistoryHandler : IRequestHandler<GetHrEmployeeHistoryQuery, HrEmployeeHistoryResult>
{
    private readonly IBookingQueryRepository queryRepository;

    public GetHrEmployeeHistoryHandler(IBookingQueryRepository queryRepository)
    {
        ArgumentNullException.ThrowIfNull(queryRepository);
        this.queryRepository = queryRepository;
    }

    public Task<HrEmployeeHistoryResult> Handle(GetHrEmployeeHistoryQuery query, CancellationToken cancellationToken)
        => queryRepository.GetEmployeeHistoryAsync(
            query.TenantId,
            query.RequestorId,
            query.From,
            query.To,
            query.StatusFilter,
            query.PageSize,
            query.Cursor,
            cancellationToken);
}
