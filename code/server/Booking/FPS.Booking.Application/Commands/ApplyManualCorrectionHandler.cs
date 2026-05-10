using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class ApplyManualCorrectionHandler : IRequestHandler<ApplyManualCorrectionCommand, ManualCorrectionResult>
{
    private readonly IBookingRepository repository;
    private readonly ICorrectionAuditRepository auditRepository;
    private readonly IEventPublisher eventPublisher;

    public ApplyManualCorrectionHandler(
        IBookingRepository repository,
        ICorrectionAuditRepository auditRepository,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(auditRepository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.auditRepository = auditRepository;
        this.eventPublisher = eventPublisher;
    }

    public async Task<ManualCorrectionResult> Handle(ApplyManualCorrectionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new BookingException("A reason is required for manual corrections.");

        var dto = await repository.GetBookingRequestAsync(command.RequestId);
        if (dto is null) throw new BookingNotFoundException(command.RequestId);

        var currentValue = GetCurrentValue(dto, command.CorrectionType);
        if (!string.Equals(currentValue, command.OldValue, StringComparison.OrdinalIgnoreCase))
            throw new CorrectionConflictException(command.RequestId, command.CorrectionType, command.OldValue, currentValue);

        var appliedAt = command.EffectiveAt ?? DateTime.UtcNow;

        var request = BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);

        request.ApplyManualCorrection(
            command.CorrectionType, command.OldValue, command.NewValue,
            command.Actor, command.Reason, appliedAt, eventPublisher);

        await PersistCorrectionAsync(dto, command, appliedAt, cancellationToken);

        await auditRepository.SaveAsync(new CorrectionAuditDto
        {
            Id = Guid.NewGuid(),
            RequestId = command.RequestId,
            TenantId = command.TenantId,
            CorrectionType = command.CorrectionType,
            OldValue = command.OldValue,
            NewValue = command.NewValue,
            Actor = command.Actor,
            Reason = command.Reason,
            AppliedAt = appliedAt
        }, cancellationToken);

        return new ManualCorrectionResult(command.RequestId, command.CorrectionType, command.NewValue, appliedAt);
    }

    private static string GetCurrentValue(BookingRequestDto dto, string correctionType) =>
        correctionType switch
        {
            "status" => dto.Status,
            "reason" => dto.RejectionReason ?? string.Empty,
            "usage" => dto.ConfirmationSource ?? string.Empty,
            "allocation" => dto.AllocatedSlotId?.ToString() ?? string.Empty,
            _ => throw new BookingException($"Unknown correction type: {correctionType}")
        };

    private async Task PersistCorrectionAsync(
        BookingRequestDto dto, ApplyManualCorrectionCommand command, DateTime appliedAt, CancellationToken cancellationToken)
    {
        switch (command.CorrectionType)
        {
            case "status":
                await repository.UpdateBookingRequestStatusAsync(
                    command.RequestId, command.NewValue, command.Reason, cancellationToken);
                break;
            case "reason":
                dto.RejectionReason = command.NewValue;
                dto.LastStatusChangedAt = appliedAt;
                await repository.CreateBookingRequestAsync(dto);
                break;
            case "usage":
                await repository.UpdateBookingRequestUsageAsync(
                    command.RequestId, command.NewValue, appliedAt, null, cancellationToken);
                break;
        }
    }
}
