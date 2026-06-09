using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Workflows;

public interface IDrawWorkflowStarter
{
    Task<DrawStartResult> StartAsync(TriggerDrawCommand command, CancellationToken cancellationToken);
}

public sealed record DrawStartResult(
    string DrawKey,
    string WorkflowInstanceId,
    string Status);     // "Started" | "AlreadyRunning"
