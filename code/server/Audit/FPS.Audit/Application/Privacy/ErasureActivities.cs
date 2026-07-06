using Dapr.Client;
using Dapr.Workflow;
using FPS.Audit.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application.Privacy;

// Each activity calls the service-owned erasure endpoint via Dapr service invocation.
// Activities are idempotent: the erasure request ID is the idempotency key each service uses.

public sealed class CheckActiveBookingsActivity(DaprClient dapr, ILogger<CheckActiveBookingsActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            var response = await dapr.InvokeMethodAsync<ServiceErasureInput, ErasureServiceResult>(
                "fairspot-booking", "erasure/check-active", input);

            logger.LogInformation(
                "Active booking check complete. ErasureRequestId={ErasureRequestId} Treatment={Treatment}",
                input.ErasureRequestId, response.Treatment);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Active booking check failed. ErasureRequestId={ErasureRequestId}",
                input.ErasureRequestId);
            return new ErasureServiceResult("booking-check", ErasureTreatment.Failed, 0, "Service unavailable");
        }
    }
}

public sealed class EraseProfileActivity(DaprClient dapr, ILogger<EraseProfileActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            var response = await dapr.InvokeMethodAsync<ServiceErasureInput, ErasureServiceResult>(
                "fairspot-profile", "erasure", input);
            logger.LogInformation("Profile erasure complete. ErasureRequestId={ErasureRequestId} Treatment={Treatment} Count={Count}",
                input.ErasureRequestId, response.Treatment, response.AffectedCount);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile erasure failed. ErasureRequestId={ErasureRequestId}", input.ErasureRequestId);
            return new ErasureServiceResult("profile", ErasureTreatment.Failed, 0, "Service unavailable");
        }
    }
}

public sealed class EraseBookingDataActivity(DaprClient dapr, ILogger<EraseBookingDataActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            var response = await dapr.InvokeMethodAsync<ServiceErasureInput, ErasureServiceResult>(
                "fairspot-booking", "erasure", input);
            logger.LogInformation("Booking erasure complete. ErasureRequestId={ErasureRequestId} Treatment={Treatment} Count={Count}",
                input.ErasureRequestId, response.Treatment, response.AffectedCount);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking erasure failed. ErasureRequestId={ErasureRequestId}", input.ErasureRequestId);
            return new ErasureServiceResult("booking", ErasureTreatment.Failed, 0, "Service unavailable");
        }
    }
}

public sealed class EraseNotificationActivity(DaprClient dapr, ILogger<EraseNotificationActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            var response = await dapr.InvokeMethodAsync<ServiceErasureInput, ErasureServiceResult>(
                "fairspot-notification", "erasure", input);
            logger.LogInformation("Notification erasure complete. ErasureRequestId={ErasureRequestId} Treatment={Treatment} Count={Count}",
                input.ErasureRequestId, response.Treatment, response.AffectedCount);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification erasure failed. ErasureRequestId={ErasureRequestId}", input.ErasureRequestId);
            return new ErasureServiceResult("notification", ErasureTreatment.Failed, 0, "Service unavailable");
        }
    }
}

public sealed class AnonymiseReportingActivity(DaprClient dapr, ILogger<AnonymiseReportingActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            var response = await dapr.InvokeMethodAsync<ServiceErasureInput, ErasureServiceResult>(
                "fairspot-reporting", "erasure", input);
            logger.LogInformation("Reporting anonymisation complete. ErasureRequestId={ErasureRequestId} Treatment={Treatment} Count={Count}",
                input.ErasureRequestId, response.Treatment, response.AffectedCount);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reporting anonymisation failed. ErasureRequestId={ErasureRequestId}", input.ErasureRequestId);
            return new ErasureServiceResult("reporting", ErasureTreatment.Failed, 0, "Service unavailable");
        }
    }
}

public sealed class ErasePiiMappingActivity(
    IPiiMappingRepository piiRepository,
    ILogger<ErasePiiMappingActivity> logger)
    : WorkflowActivity<ServiceErasureInput, ErasureServiceResult>
{
    public override async Task<ErasureServiceResult> RunAsync(WorkflowActivityContext context, ServiceErasureInput input)
    {
        try
        {
            // Audit PII mapping is deleted by actor hash (the hash is the canonical reference)
            await piiRepository.DeleteByActorHashAsync(input.TargetActorHash, input.TenantId);
            logger.LogInformation(
                "Audit PII mapping erased. ErasureRequestId={ErasureRequestId} TenantId={TenantId}",
                input.ErasureRequestId, input.TenantId);
            return new ErasureServiceResult("audit-pii", ErasureTreatment.Pseudonymised, 1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit PII mapping erasure failed. ErasureRequestId={ErasureRequestId}", input.ErasureRequestId);
            return new ErasureServiceResult("audit-pii", ErasureTreatment.Failed, 0, "Repository error");
        }
    }
}

public sealed class RecordErasureStepActivity(
    IAuditRepository auditRepository,
    ILogger<RecordErasureStepActivity> logger)
    : WorkflowActivity<ErasureStepAuditInput, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext context, ErasureStepAuditInput input)
    {
        var w = input.WorkflowInput;
        var step = input.StepResult;
        var sourceEventId = $"privacy.erasureStepRecorded:{w.ErasureRequestId}:{step.Service}";

        if (await auditRepository.ExistsAsync(sourceEventId, w.TenantId))
            return true;

        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            EventType = "privacy.erasureStepRecorded",
            EventVersion = 1,
            OccurredAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            TenantId = w.TenantId,
            CorrelationId = w.ErasureRequestId,
            ActorType = "system",
            ActorHash = w.RequestedByActorHash,
            Source = "privacy",
            EntityType = "erasureRequest",
            EntityId = w.ErasureRequestId,
            Payload = new System.Text.Json.Nodes.JsonObject
            {
                ["erasureRequestId"] = w.ErasureRequestId,
                ["targetActorHash"] = w.TargetActorHash,
                ["service"] = step.Service,
                ["treatment"] = step.Treatment,
                ["affectedCount"] = step.AffectedCount,
            },
            Action = "privacy.erasureStepRecorded",
            Result = step.Treatment,
            ReasonCode = step.Note,
            Summary = BusinessActivityMapper.ToSummary(
                "privacy.erasureStepRecorded", step.Service, step.Treatment, step.Note),
        };

        await auditRepository.AppendAsync(record);

        logger.LogInformation(
            "Erasure step recorded. ErasureRequestId={ErasureRequestId} Service={Service} Treatment={Treatment}",
            w.ErasureRequestId, step.Service, step.Treatment);

        return true;
    }
}
