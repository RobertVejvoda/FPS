using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
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

        if (isSameDay)
        {
            if (policy.SameDayBookingEnabled)
            {
                var slots = await slotService.GetAvailableSlotsAsync(
                    cmd.TenantId, cmd.LocationId ?? cmd.FacilityId, DateOnly.FromDateTime(requestedPeriod.Start),
                    requestedPeriod, cancellationToken);

                // Apply the same motorcycle preference rule as the Draw: a motorcycle
                // request takes a motorcycle-specific unit before falling back to an
                // ordinary slot. For non-motorcycle vehicles, motorcycle units are
                // already filtered out by CanAccommodate.
                sameDaySlot = slots
                    .Where(s => s.CanAccommodate(vehicle))
                    .OrderByDescending(s => s.IsMotorcycleCapacity && vehicle.Type == VehicleType.Motorcycle)
                    .FirstOrDefault();
            }

            context = SubmissionContext.CreateSameDay(
                policy.DailyRequestCap, existingCount, hasOverlap,
                sameDayEnabled: policy.SameDayBookingEnabled,
                sameDayCapacityAvailable: sameDaySlot is not null);
        }
        else
        {
            var isCutOffPassed = IsCutOffPassed(policy, requestedPeriod.Start, now);
            context = SubmissionContext.Create(policy.DailyRequestCap, existingCount, hasOverlap, isCutOffPassed);
        }

        var publishCtx = new BookingPublishContext(
            cmd.TenantId, Guid.NewGuid().ToString(), "employee", cmd.RequestorId,
            SubjectRequestorId: cmd.RequestorId,
            AllocationSource: "sameDay");
        var publisher = eventPublisher.WithContext(publishCtx);

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, publisher);

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

        await repository.CreateBookingRequestAsync(
            ToDto(request, cmd.TenantId, cmd.FacilityId, cmd.LocationId, snapshot.SnapshotVersion, vehicle, sameDaySlot));
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
            request.RejectionReason);
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
        string? locationId, string snapshotVersion, VehicleInformation vehicle, AvailableSlot? slot = null)
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
            ProfileSnapshotVersion = snapshotVersion,
            VehicleType = vehicle.Type.ToString(),
            VehicleIsElectric = vehicle.IsElectric,
            RequiresAccessibleSpot = vehicle.RequiresAccessibleSpot,
            AllocatedSlotId = slot?.SlotId.Value != null
                ? (Guid.TryParse(slot.SlotId.Value, out var slotGuid) ? slotGuid : (Guid?)null)
                : null
        };
}
