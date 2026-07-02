using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain;
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
    private readonly ITenantModulesService tenantModulesService;
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
        ITenantModulesService tenantModulesService,
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
        ArgumentNullException.ThrowIfNull(tenantModulesService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.slotService = slotService;
        this.metricsService = metricsService;
        this.policyService = policyService;
        this.profileSnapshotService = profileSnapshotService;
        this.eventPublisher = eventPublisher;
        this.tenantModulesService = tenantModulesService;
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

        // PLAT-seats (#710) — seats are a separate resource with no vehicle. Parking keeps its
        // vehicle-eligibility and license-plate rules; seats only require an active profile.
        var isSeats = cmd.ResourceType == ResourceType.Seats;

        if (snapshot.ProfileStatus != "Active" || (!isSeats && !snapshot.ParkingEligible))
        {
            logger.LogInformation(
                "Booking request rejected. TenantId={TenantId} Status=Rejected RejectionCode={RejectionCode}",
                cmd.TenantId, BookingRejectionCode.RequestorIneligible);
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.RequestorIneligible.ToString(),
                isSeats
                    ? "You are not eligible to request a seat under current policy."
                    : "You are not eligible for parking under current policy.");
        }

        // PLAT-seats (#710) — the module boundary is enforced on the server, not just the web nav:
        // a Seats request is rejected unless the tenant has the Seats module enabled, so a
        // Parking-only tenant can't create seat state by posting directly to /bookings. The lookup
        // fails closed to Parking-only.
        if (isSeats)
        {
            var enabledModules = await tenantModulesService.GetEnabledModulesAsync(cmd.TenantId, cancellationToken);
            if (!enabledModules.Contains("Seats", StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Seat request rejected — Seats module not enabled. TenantId={TenantId} RejectionCode={RejectionCode}",
                    cmd.TenantId, BookingRejectionCode.RequestorIneligible);
                return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                    BookingRejectionCode.RequestorIneligible.ToString(),
                    "The seats module is not enabled for your organisation.");
            }
        }

        var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cmd.LocationId, cancellationToken);
        var requestedPeriod = TimeSlot.Create(cmd.PlannedArrivalTime, cmd.PlannedDepartureTime);
        var requestorId = UserId.FromString(cmd.RequestorId);

        VehicleInformation vehicle;
        if (isSeats)
        {
            // A seat needs no vehicle. This sentinel carries no capabilities, so the resource-agnostic
            // Draw treats every seat slot as compatible (Tier-2 fair lottery) and every company-car /
            // vehicle-capability branch self-skips (IsCompanyCar / IsElectric / accessible all false).
            // The "SEAT" plate never leaves the service — the DTO/outcome persist no plate.
            vehicle = VehicleInformation.Create("SEAT", VehicleType.Sedan, isElectric: false, requiresAccessibleSpot: false, isCompanyCar: false);
        }
        else
        {
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

            // Profile facts take precedence over request body fields
            if (!TryMapVehicleType(profileVehicle.VehicleType, out var mappedVehicleType))
            {
                logger.LogWarning(
                    "Booking request rejected. TenantId={TenantId} Status=Rejected RejectionCode={RejectionCode} VehicleType={VehicleType}",
                    cmd.TenantId, BookingRejectionCode.VehicleConstraintUnmatched, profileVehicle.VehicleType);
                return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                    BookingRejectionCode.VehicleConstraintUnmatched.ToString(),
                    $"Vehicle type '{profileVehicle.VehicleType}' is not supported for booking.");
            }

            vehicle = VehicleInformation.Create(
                profileVehicle.LicensePlate,
                mappedVehicleType,
                profileVehicle.IsElectric,
                snapshot.AccessibilityEligible,
                snapshot.HasCompanyCar);
        }

        var now = clock.GetTenantUtcNow(cmd.TenantId);
        var isSameDay = IsSameDay(policy, requestedPeriod.Start, now);
        var existingCount = await repository.CountRequestsForDateAsync(
            cmd.TenantId, requestedPeriod.Start.Date, cancellationToken);
        var hasOverlap = await repository.HasOverlappingRequestAsync(
            cmd.TenantId, cmd.RequestorId, requestedPeriod, cancellationToken);

        SubmissionContext context;
        AvailableSlot? sameDaySlot = null;
        bool sameDayIsFixedSlot = false;
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
                    sameDayIsFixedSlot = fixedSlot.Slot is not null;
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
            LocationId: cmd.LocationId ?? cmd.FacilityId,
            // Seats carry no vehicle facts into evidence — only the resource type.
            VehicleLicensePlate: isSeats ? null : vehicle.LicensePlate,
            VehicleType: isSeats ? null : vehicle.Type.ToString(),
            VehicleIsElectric: !isSeats && vehicle.IsElectric,
            ResourceType: cmd.ResourceType.ToString());
        var publisher = eventPublisher.WithContext(publishCtx);

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, publisher);
        var effectiveRejectionReason = request.RejectionReason;

        if (isSameDay && request.Status == BookingRequestStatus.Pending && sameDaySlot is not null)
        {
            request.Allocate(eventPublisher.WithContext(publishCtx with { AllocationSource = "sameDay" }));

            // Skip metrics only for genuine Tier 1 fixed-slot allocations.
            // Company-car employees allocated through normal same-day lookup (no fixed slot found)
            // are not Tier 1 guaranteed and must increment fairness history like other same-day wins.
            if (!sameDayIsFixedSlot)
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
            // AllocationSource distinguishes this from same-day allocations in DataHub/audit.
            request.Allocate(eventPublisher.WithContext(publishCtx with { AllocationSource = "companyCarFixedSlot" }));
        }

        await repository.CreateBookingRequestAsync(
            ToDto(request, cmd.TenantId, cmd.FacilityId, cmd.LocationId, snapshot.SnapshotVersion, vehicle,
                cmd.ResourceType, sameDaySlot ?? scheduledCompanyCarSlot, effectiveRejectionReason));
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

    // Maps Profile/HR vehicle type strings to Booking VehicleType.
    // Handles canonical enum names (case-insensitive) and HR import aliases (e.g. "car" → Sedan).
    private static bool TryMapVehicleType(string value, out VehicleType result)
    {
        if (Enum.TryParse(value, ignoreCase: true, out result))
            return true;

        switch (value.ToLowerInvariant())
        {
            case "car": result = VehicleType.Sedan; return true;
            default: result = default; return false;
        }
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
        string? locationId, string snapshotVersion, VehicleInformation vehicle, ResourceType resourceType,
        AvailableSlot? slot = null, string? rejectionReason = null)
    {
        var isSeats = resourceType == ResourceType.Seats;
        return new()
        {
            RequestId = request.Id.Value,
            TenantId = tenantId,
            VehicleId = Guid.Empty,
            FacilityId = Guid.TryParse(facilityId, out var fid) ? fid : Guid.Empty,
            LocationId = locationId ?? facilityId,
            ResourceType = resourceType.ToString(),
            PlannedArrivalTime = request.RequestedPeriod.Start,
            PlannedDepartureTime = request.RequestedPeriod.End,
            RequestedBy = request.RequestorId.Value.ToString(),
            RequestedAt = request.SubmittedAt,
            Status = request.Status.ToString(),
            RejectionCode = request.RejectionCode?.ToString(),
            RejectionReason = rejectionReason ?? request.RejectionReason,
            ProfileSnapshotVersion = snapshotVersion,
            // Seats persist no vehicle facts.
            VehicleType = isSeats ? null : vehicle.Type.ToString(),
            VehicleIsElectric = !isSeats && vehicle.IsElectric,
            RequiresAccessibleSpot = !isSeats && vehicle.RequiresAccessibleSpot,
            VehicleIsCompanyCar = !isSeats && vehicle.IsCompanyCar,
            // Slot id is a free-form string (e.g. "M1-1" for motorcycle units), not a Guid.
            AllocatedSlotId = slot?.SlotId.Value
        };
    }
}
