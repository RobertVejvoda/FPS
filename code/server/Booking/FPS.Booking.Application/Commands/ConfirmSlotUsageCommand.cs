using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record ConfirmSlotUsageCommand(
    Guid RequestId,
    string TenantId,
    string RequestorId,
    string ConfirmationSource,
    DateTime? ConfirmedAt,
    string? SourceEventId) : IRequest<ConfirmSlotUsageResult>;
