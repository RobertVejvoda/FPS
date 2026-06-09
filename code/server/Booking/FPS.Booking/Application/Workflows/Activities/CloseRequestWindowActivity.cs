using Dapr.Workflow;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

// Records that the request window is closed for this Draw key.
// Full window locking (preventing late submission) is enforced at submission time;
// this step records the intent in the lifecycle audit trail.
public sealed class CloseRequestWindowActivity(IDrawRepository drawRepository)
    : WorkflowActivity<DrawAttemptContext, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext context, DrawAttemptContext input)
    {
        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "RequestWindowClosed", "Completed",
            summary: "Request window closed; new submissions for this Draw key are rejected at submission time.");
        return true;
    }
}
