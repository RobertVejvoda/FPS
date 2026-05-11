using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Profile;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class SubmitBookingRequestHandler : IRequestHandler<SubmitBookingRequestCommand, SubmitBookingRequestResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IAvailableSlotService slotService;
    private readonly IEmployeeMetricsService metricsService;
    private readonly ITenantPolicyService policyService;
    private readonly IProfileSnapshotService profileSnapshotService;
    private readonly IEventPublisher eventPublisher;

    public SubmitBookingRequestHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IAvailableSlotService slotService,
        IEmployeeMetricsService metricsService,
        ITenantPolicyService policyService,
        IProfileSnapshotService profileSnapshotService,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(metricsService);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(profileSnapshotService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.slotService = slotService;
        this.metricsService = metricsService;
        this.policyService = policyService;
        this.profileSnapshotService = profileSnapshotService;
        this.eventPublisher = eventPublisher;
    }

    public async Task<SubmitBookingRequestResult> Handle(
        SubmitBookingRequestCommand cmd,
        CancellationToken cancellationToken)
    {
        var snapshot = await profileSnapshotService.GetSnapshotAsync(cmd.TenantId, cmd.RequestorId, cancellationToken);
        if (snapshot is null)
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.ProfileUnavailable.ToString(),
                "Profile data is unavailable. Please try again later.");

        if (snapshot.ProfileStatus != "Active" || !snapshot.ParkingEligible)
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.RequestorIneligible.ToString(),
                "You are not eligible for parking under current policy.");

        var profileVehicle = snapshot.Vehicles.FirstOrDefault(v =>
            v.LicensePlate.Equals(cmd.LicensePlate, StringComparison.OrdinalIgnoreCase) && v.IsActive);
        if (profileVehicle is null)
            return new SubmitBookingRequestResult(Guid.Empty, "Rejected",
                BookingRejectionCode.VehicleConstraintUnmatched.ToString(),
                "The requested vehicle is not registered or is inactive in your profile.");

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

        if (isSameDay && request.Status == BookingRequestStatus.Pending && sameDaySlot is not null)
        {
            request.Allocate(eventPublisher);

            if (!snapshot.HasCompanyCar)
            {
                await metricsService.IncrementRecentAllocationAsync(
                    cmd.TenantId, cmd.RequestorId,
                    DateOnly.FromDateTime(requestedPeriod.Start), cancellationToken);
            }
        }

        await repository.CreateBookingRequestAsync(
            ToDto(request, cmd.TenantId, cmd.FacilityId, snapshot.SnapshotVersion, sameDaySlot));
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

    private static BookingRequestDto ToDto(BookingRequest request, string tenantId, string facilityId,
        string snapshotVersion, AvailableSlot? slot = null)
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
            ProfileSnapshotVersion = snapshotVersion,
            AllocatedSlotId = slot?.SlotId.Value != null
                ? (Guid.TryParse(slot.SlotId.Value, out var slotGuid) ? slotGuid : (Guid?)null)
                : null
        };
}
