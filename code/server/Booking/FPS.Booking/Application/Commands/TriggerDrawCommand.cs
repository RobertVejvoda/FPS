using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record TriggerDrawCommand(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason,
    // Caller must supply the actor identity (authenticated user for manual
    // and recovery, scheduler identity for cron). Previously defaulted to
    // a static "hr-admin", which masked the real operator on every run —
    // Codex review on PR #492.
    string TriggeredBy,
    string TriggerSource = "manual",
    bool AllowRecovery = false,
    string? WorkflowInstanceIdOverride = null) : IRequest<TriggerDrawResult>;
