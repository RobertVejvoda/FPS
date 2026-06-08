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
        // Tenant default policy (no location) is used for window/paging; location overrides apply per item below
        var tenantPolicy = await policyService.GetEffectivePolicyAsync(query.TenantId, cancellationToken: cancellationToken);
        var pageSize = Math.Min(Math.Max(1, query.PageSize), MaxPageSize);
        var from = query.From ?? DateOnly.FromDateTime(clock.GetTenantUtcNow(query.TenantId).UtcDateTime.AddDays(-tenantPolicy.AllocationLookbackDays));

        var result = await queryRepository.GetByRequestorAsync(
            query.TenantId,
            query.RequestorId,
            from,
            query.To,
            query.Status,
            pageSize,
            query.Cursor,
            cancellationToken);

        // Suppress confirmUsage per item using the location-effective policy.
        // Cache policy per unique LocationId to avoid redundant calls.
        var policyCache = new Dictionary<string, TenantPolicy>(StringComparer.OrdinalIgnoreCase);
        var items = new BookingListItem[result.Items.Count];
        for (var i = 0; i < result.Items.Count; i++)
        {
            var item = result.Items[i];
            if (item.NextAction == "confirmUsage")
            {
                var cacheKey = item.LocationId ?? string.Empty;
                if (!policyCache.TryGetValue(cacheKey, out var locPolicy))
                {
                    locPolicy = await policyService.GetEffectivePolicyAsync(query.TenantId, item.LocationId, cancellationToken);
                    policyCache[cacheKey] = locPolicy;
                }
                items[i] = locPolicy.UsageConfirmationEnabled ? item : item with { NextAction = "none" };
            }
            else
            {
                items[i] = item;
            }
        }

        return result with { Items = items };
    }
}
