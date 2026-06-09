namespace FPS.Booking.IntegrationTests;

// Dapr workflow integration tests will be implemented in Phase 1.
public class DaprWorkflowTests
{
    [Fact(Skip = "Requires running Dapr sidecar — implement in Phase 1")]
    public Task BookingWorkflow_HappyPath_AllocatesSlot() => Task.CompletedTask;
}
