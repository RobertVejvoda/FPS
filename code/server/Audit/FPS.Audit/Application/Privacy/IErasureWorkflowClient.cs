using Dapr.Workflow;

namespace FPS.Audit.Application.Privacy;

public interface IErasureWorkflowClient
{
    Task<string> ScheduleAsync(string instanceId, ErasureWorkflowInput input);
    Task<WorkflowState> GetStateAsync(string instanceId);
}

public sealed class DaprErasureWorkflowClient(DaprWorkflowClient inner) : IErasureWorkflowClient
{
    public Task<string> ScheduleAsync(string instanceId, ErasureWorkflowInput input) =>
        inner.ScheduleNewWorkflowAsync(nameof(ErasureWorkflow), instanceId, input);

    public Task<WorkflowState> GetStateAsync(string instanceId) =>
        inner.GetWorkflowStateAsync(instanceId, getInputsAndOutputs: true);
}
