using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.SharedKernel.Time;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetMyBookingsHandler : IRequestHandler<GetMyBookingsQuery, BookingListResult>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 50;

    private readonly IBookingQueryRepository queryRepository;
    private readonly ITenantPolicyService policyService;
    private readonly ISystemClock clock;

    public GetMyBookingsHandler(IBookingQueryRepository queryRepository, ITenantPolicyService policyService, ISystemClock clock)
    {
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(clock);
        this.queryRepository = queryRepository;
        this.policyService = policyService;
        this.clock = clock;
    }

    public async Task<BookingListResult> Handle(GetMyBookingsQuery query, CancellationToken cancellationToken)
    {
        var policy = await policyService.GetEffectivePolicyAsync(query.TenantId, cancellationToken: cancellationToken);
        var pageSize = Math.Min(Math.Max(1, query.PageSize), MaxPageSize);
        var from = query.From ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(-policy.AllocationLookbackDays));

        return await queryRepository.GetByRequestorAsync(
            query.TenantId,
            query.RequestorId,
            from,
            query.To,
            query.Status,
            pageSize,
            query.Cursor,
            cancellationToken);
    }
}
