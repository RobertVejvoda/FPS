using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class SubmitBookingRequestHandler : IRequestHandler<SubmitBookingRequestCommand, SubmitBookingRequestResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IAvailableSlotService slotService;
    private readonly IEmployeeMetricsService metricsService;
    private readonly ITenantPolicyService policyService;
    private readonly IEventPublisher eventPublisher;

    public SubmitBookingRequestHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IAvailableSlotService slotService,
        IEmployeeMetricsService metricsService,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(metricsService);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.slotService = slotService;
        this.metricsService = metricsService;
        this.policyService = policyService;
        this.eventPublisher = eventPublisher;
    }

    public async Task<SubmitBookingRequestResult> Handle(
        SubmitBookingRequestCommand cmd,
        CancellationToken cancellationToken)
    {
        var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cmd.LocationId, cancellationToken);
        var requestedPeriod = TimeSlot.Create(cmd.PlannedArrivalTime, cmd.PlannedDepartureTime);
        var requestorId = UserId.FromString(cmd.RequestorId);
        var vehicle = VehicleInformation.Create(
            cmd.LicensePlate,
            Enum.Parse<VehicleType>(cmd.VehicleType, ignoreCase: true),
            cmd.IsElectric, cmd.RequiresAccessibleSpot, cmd.IsCompanyCar);

        var isSameDay = IsSameDay(policy, requestedPeriod.Start);
        var existingCount = await repository.CountRequestsForDateAsync(
            cmd.TenantId, requestedPeriod.Start.Date, cancellationToken);
        var hasOverlap = await repository.HasOverlappingRequestAsync(
            cmd.TenantId, cmd.RequestorId, requestedPeriod, cancellationToken);

        SubmissionContext context;
        AvailableSlot? sameDaySlot = null;

        if (isSameDay)
        {
            if (policy.SameDayBookingEnabled)
            {
                var slots = await slotService.GetAvailableSlotsAsync(
                    cmd.TenantId, cmd.FacilityId, DateOnly.FromDateTime(requestedPeriod.Start),
                    requestedPeriod, cancellationToken);

                sameDaySlot = slots.FirstOrDefault(s => s.CanAccommodate(vehicle));
            }

            context = SubmissionContext.CreateSameDay(
                policy.DailyRequestCap, existingCount, hasOverlap,
                sameDayEnabled: policy.SameDayBookingEnabled,
                sameDayCapacityAvailable: sameDaySlot is not null);
        }
        else
        {
            var isCutOffPassed = IsCutOffPassed(policy, requestedPeriod.Start);
            context = SubmissionContext.Create(policy.DailyRequestCap, existingCount, hasOverlap, isCutOffPassed);
        }

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, eventPublisher);

        // Same-day: immediately allocate when domain validated successfully
        if (isSameDay && request.Status == BookingRequestStatus.Pending && sameDaySlot is not null)
        {
            request.Allocate(eventPublisher);

            // Increment metrics for non-company-car same-day allocations
            if (!cmd.IsCompanyCar)
            {
                await metricsService.IncrementRecentAllocationAsync(
                    cmd.TenantId, cmd.RequestorId,
                    DateOnly.FromDateTime(requestedPeriod.Start), cancellationToken);
            }
        }

        await repository.CreateBookingRequestAsync(ToDto(request, cmd.TenantId, cmd.FacilityId, sameDaySlot));
        await queryRepository.AddToUserIndexAsync(cmd.TenantId, cmd.RequestorId, request.Id.Value, cancellationToken);

        return new SubmitBookingRequestResult(
            request.Id.Value,
            request.Status.ToString(),
            request.RejectionCode?.ToString(),
            request.RejectionReason);
    }

    private static bool IsSameDay(TenantPolicy policy, DateTime requestedStart)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return requestedStart.Date == nowLocal.Date;
    }

    private static bool IsCutOffPassed(TenantPolicy policy, DateTime requestedStart)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        if (requestedStart.Date > nowLocal.Date.AddDays(1))
            return false;

        return TimeOnly.FromDateTime(nowLocal) >= policy.DrawCutOffTime;
    }

    private static BookingRequestDto ToDto(BookingRequest request, string tenantId, string facilityId, AvailableSlot? slot = null)
        => new()
        {
            RequestId = request.Id.Value,
            VehicleId = Guid.Empty,
            FacilityId = Guid.Parse(facilityId),
            LocationId = facilityId,
            PlannedArrivalTime = request.RequestedPeriod.Start,
            PlannedDepartureTime = request.RequestedPeriod.End,
            RequestedBy = request.RequestorId.Value.ToString(),
            RequestedAt = request.SubmittedAt,
            Status = request.Status.ToString(),
            AllocatedSlotId = slot?.SlotId.Value != null
                ? (Guid.TryParse(slot.SlotId.Value, out var slotGuid) ? slotGuid : (Guid?)null)
                : null
        };
}
