using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetMyBookingsHandler : IRequestHandler<GetMyBookingsQuery, BookingListResult>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 50;

    private readonly IBookingQueryRepository queryRepository;
    private readonly ITenantPolicyService policyService;

    public GetMyBookingsHandler(IBookingQueryRepository queryRepository, ITenantPolicyService policyService)
    {
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        this.queryRepository = queryRepository;
        this.policyService = policyService;
    }

    public async Task<BookingListResult> Handle(GetMyBookingsQuery query, CancellationToken cancellationToken)
    {
        var policy = await policyService.GetEffectivePolicyAsync(query.TenantId, cancellationToken: cancellationToken);
        var pageSize = Math.Min(Math.Max(1, query.PageSize), MaxPageSize);
        var from = query.From ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-policy.AllocationLookbackDays));

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
