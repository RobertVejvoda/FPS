using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public class SubmitBookingRequestHandler : IRequestHandler<SubmitBookingRequestCommand, SubmitBookingRequestResult>
{
    private readonly IBookingRepository _repository;
    private readonly ITenantPolicyService _policyService;
    private readonly IEventPublisher _eventPublisher;

    public SubmitBookingRequestHandler(
        IBookingRepository repository,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher)
    {
        _repository = repository;
        _policyService = policyService;
        _eventPublisher = eventPublisher;
    }

    public async Task<SubmitBookingRequestResult> Handle(
        SubmitBookingRequestCommand cmd,
        CancellationToken cancellationToken)
    {
        var policy = await _policyService.GetEffectivePolicyAsync(cmd.TenantId, cmd.LocationId, cancellationToken);

        var requestedPeriod = TimeSlot.Create(cmd.PlannedArrivalTime, cmd.PlannedDepartureTime);
        var requestorId = UserId.FromString(cmd.RequestorId);

        // Resolve policy-dependent context values
        var isCutOffPassed = IsCutOffPassed(policy, requestedPeriod.Start);
        var existingCount = await _repository.CountRequestsForDateAsync(
            cmd.TenantId, requestedPeriod.Start.Date, cancellationToken);
        var hasOverlap = await _repository.HasOverlappingRequestAsync(
            cmd.TenantId, cmd.RequestorId, requestedPeriod, cancellationToken);

        var context = SubmissionContext.Create(
            policy.DailyRequestCap,
            existingCount,
            hasOverlap,
            isCutOffPassed);

        var vehicle = VehicleInformation.Create(
            cmd.LicensePlate,
            Enum.Parse<VehicleType>(cmd.VehicleType, ignoreCase: true),
            cmd.IsElectric,
            cmd.RequiresAccessibleSpot,
            cmd.IsCompanyCar);

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, _eventPublisher);

        await _repository.CreateBookingRequestAsync(ToDto(request, cmd.TenantId, cmd.FacilityId));

        return new SubmitBookingRequestResult(
            request.Id.Value,
            request.Status.ToString(),
            request.RejectionCode?.ToString(),
            request.RejectionReason);
    }

    private static bool IsCutOffPassed(TenantPolicy policy, DateTime requestedStart)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        // Cut-off applies to requests for today's draw; future dates are always open
        if (requestedStart.Date > nowLocal.Date.AddDays(1))
            return false;

        return TimeOnly.FromDateTime(nowLocal) >= policy.DrawCutOffTime;
    }

    private static BookingRequestDto ToDto(BookingRequest request, string tenantId, string facilityId)
        => new()
        {
            RequestId = request.Id.Value,
            VehicleId = Guid.Empty,   // vehicle ID resolution added in later slice
            FacilityId = Guid.Parse(facilityId),
            PlannedArrivalTime = request.RequestedPeriod.Start,
            PlannedDepartureTime = request.RequestedPeriod.End,
            RequestedBy = request.RequestorId.Value.ToString(),
            RequestedAt = request.SubmittedAt,
            Status = request.Status.ToString()
        };
}
