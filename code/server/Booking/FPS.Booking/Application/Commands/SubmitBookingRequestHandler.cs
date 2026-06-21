using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Services;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Profile;
using FPS.SharedKernel.Time;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FPS.Booking.Application.Commands;

public sealed class SubmitBookingRequestHandler : IRequestHandler<SubmitBookingRequestCommand, SubmitBookingRequestResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IAvailableSlotService slotService;
    private readonly IEmployeeMetricsService metricsService;
    private readonly ITenantPolicyService policyService;
    private readonly IProfileSnapshotService profileSnapshotService;
    private readonly IBookingEventPublisher eventPublisher;
    private readonly ILogger<SubmitBookingRequestHandler> logger;
    private readonly ISystemClock clock;

    public SubmitBookingRequestHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IAvailableSlotService slotService,
        IEmployeeMetricsService metricsService,
        ITenantPolicyService policyService,
        IProfileSnapshotService profileSnapshotService,
        IBookingEventPublisher eventPublisher,
        ILogger<SubmitBookingRequestHandler> logger,
        ISystemClock clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(metricsService);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(profileSnapshotService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.slotService = slotService;
        this.metricsService = metricsService;
        this.policyService = policyService;
        this.profileSnapshotService = profileSnapshotService;
        this.eventPublisher = eventPublisher;
        this.logger = logger;
        this.clock = clock;
    }

    public async Task<SubmitBookingRequestResult> Handle(
        SubmitBookingRequestCommand cmd,
        CancellationToken cancellationToken)
    {
        var snapshot = await profileSnapshotService.GetSnapshotAsync(cmd.TenantId, cmd.RequestorId, cancellationToken);
        if (snapshot is null)
        {
            logger.LogWarning(
                "Booking request rejected. TenantId={TenantId} Status=Rejected RejectionCode={RejectionCode}",
                cmd.TenantId, BookingRejectionCode.ProfileUnavailable);
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.ProfileUnavailable.ToString(),
                "Profile data is unavailable. Please try again later.");
        }

        if (snapshot.ProfileStatus != "Active" || !snapshot.ParkingEligible)
        {
            logger.LogInformation(
                "Booking request rejected. TenantId={TenantId} Status=Rejected RejectionCode={RejectionCode}",
                cmd.TenantId, BookingRejectionCode.RequestorIneligible);
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.RequestorIneligible.ToString(),
                "You are not eligible for parking under current policy.");
        }

        var profileVehicle = snapshot.Vehicles.FirstOrDefault(v =>
            v.LicensePlate.Equals(cmd.LicensePlate, StringComparison.OrdinalIgnoreCase) && v.IsActive);
        if (profileVehicle is null)
        {
            logger.LogInformation(
                "Booking request rejected. TenantId={TenantId} Status=Rejected RejectionCode={RejectionCode}",
                cmd.TenantId, BookingRejectionCode.VehicleConstraintUnmatched);
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.VehicleConstraintUnmatched.ToString(),
                "The requested vehicle is not registered or is inactive in your profile.");
        }

        var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cmd.LocationId, cancellationToken);
        var requestedPeriod = TimeSlot.Create(cmd.PlannedArrivalTime, cmd.PlannedDepartureTime);
        var requestorId = UserId.FromString(cmd.RequestorId);

        // Profile facts take precedence over request body fields
        var vehicle = VehicleInformation.Create(
            profileVehicle.LicensePlate,
            Enum.Parse<VehicleType>(profileVehicle.VehicleType, ignoreCase: true),
            profileVehicle.IsElectric,
            snapshot.AccessibilityEligible,
            snapshot.HasCompanyCar);

        var now = clock.GetTenantUtcNow(cmd.TenantId);
        var isSameDay = IsSameDay(policy, requestedPeriod.Start, now);
        var existingCount = await repository.CountRequestsForDateAsync(
            cmd.TenantId, requestedPeriod.Start.Date, cancellationToken);
        var hasOverlap = await repository.HasOverlappingRequestAsync(
            cmd.TenantId, cmd.RequestorId, requestedPeriod, cancellationToken);

        SubmissionContext context;
        AvailableSlot? sameDaySlot = null;
        // For scheduled (non-same-day) company-car requests: resolved fixed slot for immediate Tier 1 allocation.
        AvailableSlot? scheduledCompanyCarSlot = null;

        if (isSameDay)
        {
            if (policy.SameDayBookingEnabled)
            {
                var slots = await slotService.GetAvailableSlotsAsync(
                    cmd.TenantId, cmd.LocationId ?? cmd.FacilityId, DateOnly.FromDateTime(requestedPeriod.Start),
                    requestedPeriod, cancellationToken);

                if (vehicle.IsCompanyCar)
                {
                    var fixedSlot = CompanyCarReservedSlotRules.Resolve(requestorId, vehicle, slots);
                    sameDaySlot = fixedSlot.Slot;
                }

                // When no fixed slot was found (or not a company-car), fall through to normal slot lookup.
                // Apply the same motorcycle preference rule as the Draw: a motorcycle request takes a
                // motorcycle-specific unit before falling back to an ordinary slot.
                if (sameDaySlot is null)
                {
                    sameDaySlot = slots
                        .Where(s => string.IsNullOrWhiteSpace(s.ReservedForUserId))
                        .Where(s => s.CanAccommodate(vehicle))
                        .OrderByDescending(s => s.IsMotorcycleCapacity && vehicle.Type == VehicleType.Motorcycle)
                        .FirstOrDefault();
                }
            }

            context = SubmissionContext.CreateSameDay(
                policy.DailyRequestCap, existingCount, hasOverlap,
                sameDayEnabled: policy.SameDayBookingEnabled,
                sameDayCapacityAvailable: sameDaySlot is not null);
        }
        else
        {
            var isCutOffPassed = IsCutOffPassed(policy, requestedPeriod.Start, now);

            // For company-car employees submitting a scheduled request before cut-off,
            // resolve the HR-assigned fixed slot for immediate Tier 1 allocation.
            // If the slot is missing, inactive, incompatible, or already consumed the
            // request stays Pending and enters the normal Draw (Tier 2 lottery).
            if (vehicle.IsCompanyCar && !isCutOffPassed)
            {
                var slots = await slotService.GetAvailableSlotsAsync(
                    cmd.TenantId, cmd.LocationId ?? cmd.FacilityId,
                    DateOnly.FromDateTime(requestedPeriod.Start), requestedPeriod, cancellationToken);
                var fixedSlot = CompanyCarReservedSlotRules.Resolve(requestorId, vehicle, slots);
                scheduledCompanyCarSlot = fixedSlot.Slot;
            }

            context = SubmissionContext.Create(policy.DailyRequestCap, existingCount, hasOverlap, isCutOffPassed);
        }

        var publishCtx = new BookingPublishContext(
            cmd.TenantId, Guid.NewGuid().ToString(), "employee", cmd.RequestorId,
            SubjectRequestorId: cmd.RequestorId,
            AllocationSource: "sameDay");
        var publisher = eventPublisher.WithContext(publishCtx);

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, publisher);
        var effectiveRejectionReason = request.RejectionReason;

        if (isSameDay && request.Status == BookingRequestStatus.Pending && sameDaySlot is not null)
        {
            request.Allocate(publisher);

            if (!snapshot.HasCompanyCar)
            {
                await metricsService.IncrementRecentAllocationAsync(
                    cmd.TenantId, cmd.RequestorId,
                    DateOnly.FromDateTime(requestedPeriod.Start), cancellationToken);
            }
        }
        else if (!isSameDay && request.Status == BookingRequestStatus.Pending && scheduledCompanyCarSlot is not null)
        {
            // Tier 1 guaranteed fixed-slot allocation for scheduled company-car requests.
            // Allocate immediately; do not increment Tier 2 fairness metrics.
            // The slot is passed to ToDto() below (same pattern as same-day allocation);
            // AllocatedSlotId lives in the DTO layer, not the domain aggregate.
            request.Allocate(publisher);
        }

        await repository.CreateBookingRequestAsync(
            ToDto(request, cmd.TenantId, cmd.FacilityId, cmd.LocationId, snapshot.SnapshotVersion, vehicle,
                sameDaySlot ?? scheduledCompanyCarSlot, effectiveRejectionReason));
        await queryRepository.AddToUserIndexAsync(cmd.TenantId, cmd.RequestorId, request.Id.Value, cancellationToken);
        await queryRepository.AddToTenantOpsIndexAsync(cmd.TenantId, request.Id.Value, cancellationToken);
        if (request.Status == BookingRequestStatus.Pending)
        {
            await queryRepository.AddToTenantPendingIndexAsync(cmd.TenantId, request.Id.Value, cancellationToken);
        }

        logger.LogInformation(
            "Booking request submitted. TenantId={TenantId} BookingRequestId={BookingRequestId} Status={Status} RejectionCode={RejectionCode}",
            cmd.TenantId, request.Id.Value, request.Status, request.RejectionCode);

        return new SubmitBookingRequestResult(
            request.Id.Value,
            request.Status.ToString(),
            request.RejectionCode?.ToString(),
            effectiveRejectionReason);
    }

    private static bool IsSameDay(TenantPolicy policy, DateTime requestedStart, DateTimeOffset now)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(now.UtcDateTime, tz);
        return requestedStart.Date == nowLocal.Date;
    }

    private static bool IsCutOffPassed(TenantPolicy policy, DateTime requestedStart, DateTimeOffset now)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(now.UtcDateTime, tz);

        if (requestedStart.Date > nowLocal.Date.AddDays(1))
            return false;

        return TimeOnly.FromDateTime(nowLocal) >= policy.DrawCutOffTime;
    }

    private static BookingRequestDto ToDto(BookingRequest request, string tenantId, string facilityId,
        string? locationId, string snapshotVersion, VehicleInformation vehicle, AvailableSlot? slot = null,
        string? rejectionReason = null)
        => new()
        {
            RequestId = request.Id.Value,
            TenantId = tenantId,
            VehicleId = Guid.Empty,
            FacilityId = Guid.Parse(facilityId),
            LocationId = locationId ?? facilityId,
            PlannedArrivalTime = request.RequestedPeriod.Start,
            PlannedDepartureTime = request.RequestedPeriod.End,
            RequestedBy = request.RequestorId.Value.ToString(),
            RequestedAt = request.SubmittedAt,
            Status = request.Status.ToString(),
            RejectionCode = request.RejectionCode?.ToString(),
            RejectionReason = rejectionReason ?? request.RejectionReason,
            ProfileSnapshotVersion = snapshotVersion,
            VehicleType = vehicle.Type.ToString(),
            VehicleIsElectric = vehicle.IsElectric,
            RequiresAccessibleSpot = vehicle.RequiresAccessibleSpot,
            VehicleIsCompanyCar = vehicle.IsCompanyCar,
            // Slot id is a free-form string (e.g. "M1-1" for motorcycle units), not a Guid.
            AllocatedSlotId = slot?.SlotId.Value
        };
}
