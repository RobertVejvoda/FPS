using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record ApplyManualCorrectionCommand(
    Guid RequestId,
    string TenantId,
    string Actor,
    string CorrectionType,
    string OldValue,
    string NewValue,
    string Reason,
    DateTime? EffectiveAt) : IRequest<ManualCorrectionResult>;
