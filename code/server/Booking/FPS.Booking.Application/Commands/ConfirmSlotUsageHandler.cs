using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class ConfirmSlotUsageHandler : IRequestHandler<ConfirmSlotUsageCommand, ConfirmSlotUsageResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingEventPublisher eventPublisher;
    private readonly ITenantPolicyService policyService;

    public ConfirmSlotUsageHandler(IBookingRepository repository, IBookingEventPublisher eventPublisher, ITenantPolicyService policyService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(policyService);
        this.repository = repository;
        this.eventPublisher = eventPublisher;
        this.policyService = policyService;
    }

    public async Task<ConfirmSlotUsageResult> Handle(ConfirmSlotUsageCommand command, CancellationToken cancellationToken)
    {
        // Load booking first so the location-aware effective policy can be resolved
        var dto = await repository.GetBookingRequestAsync(command.TenantId, command.RequestId);
        if (dto is null) throw new BookingNotFoundException(command.RequestId);

        var policy = await policyService.GetEffectivePolicyAsync(command.TenantId, dto.LocationId, cancellationToken);
        if (!policy.UsageConfirmationEnabled)
            throw new BookingException("Usage confirmation is not enabled for this location.");

        var confirmedAt = command.ConfirmedAt ?? DateTime.UtcNow;
        var source = Enum.Parse<ConfirmationSource>(command.ConfirmationSource, ignoreCase: true);

        // Idempotency: already Used with same source → return existing state
        if (dto.Status == "Used")
        {
            return new ConfirmSlotUsageResult(
                command.RequestId,
                "Used",
                dto.UsageConfirmedAt ?? confirmedAt,
                WasAlreadyConfirmed: true);
        }

        var request = BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);

        var publisher = eventPublisher.WithContext(new BookingPublishContext(
            command.TenantId, Guid.NewGuid().ToString(), "system", null,
            SubjectRequestorId: dto.RequestedBy));
        request.ConfirmUsage(source, confirmedAt, publisher);

        await repository.UpdateBookingRequestUsageAsync(
            command.TenantId,
            command.RequestId,
            command.ConfirmationSource,
            confirmedAt,
            command.SourceEventId,
            cancellationToken);

        return new ConfirmSlotUsageResult(command.RequestId, "Used", confirmedAt, WasAlreadyConfirmed: false);
    }
}
