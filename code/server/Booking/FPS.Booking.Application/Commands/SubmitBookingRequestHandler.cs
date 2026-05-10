using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;
using System.Threading;

namespace FPS.Booking.Application.Commands;

public sealed class SubmitBookingRequestHandler : IRequestHandler<SubmitBookingRequestCommand, SubmitBookingRequestResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly ITenantPolicyService policyService;
    private readonly IEventPublisher eventPublisher;

    public SubmitBookingRequestHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.queryRepository = queryRepository;
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

        var isCutOffPassed = IsCutOffPassed(policy, requestedPeriod.Start);
        var existingCount = await repository.CountRequestsForDateAsync(
            cmd.TenantId, requestedPeriod.Start.Date, cancellationToken);
        var hasOverlap = await repository.HasOverlappingRequestAsync(
            cmd.TenantId, cmd.RequestorId, requestedPeriod, cancellationToken);

        var context = SubmissionContext.Create(
            policy.DailyRequestCap, existingCount, hasOverlap, isCutOffPassed);

        var vehicle = VehicleInformation.Create(
            cmd.LicensePlate,
            Enum.Parse<VehicleType>(cmd.VehicleType, ignoreCase: true),
            cmd.IsElectric,
            cmd.RequiresAccessibleSpot,
            cmd.IsCompanyCar);

        var request = BookingRequest.Submit(requestorId, requestedPeriod, vehicle, context, eventPublisher);

        await repository.CreateBookingRequestAsync(ToDto(request, cmd.TenantId, cmd.FacilityId));
        await queryRepository.AddToUserIndexAsync(cmd.TenantId, cmd.RequestorId, request.Id.Value, cancellationToken);

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

        if (requestedStart.Date > nowLocal.Date.AddDays(1))
            return false;

        return TimeOnly.FromDateTime(nowLocal) >= policy.DrawCutOffTime;
    }

    private static BookingRequestDto ToDto(BookingRequest request, string tenantId, string facilityId)
        => new()
        {
            RequestId = request.Id.Value,
            VehicleId = Guid.Empty,
            FacilityId = Guid.Parse(facilityId),
            PlannedArrivalTime = request.RequestedPeriod.Start,
            PlannedDepartureTime = request.RequestedPeriod.End,
            RequestedBy = request.RequestorId.Value.ToString(),
            RequestedAt = request.SubmittedAt,
            Status = request.Status.ToString()
        };
}
