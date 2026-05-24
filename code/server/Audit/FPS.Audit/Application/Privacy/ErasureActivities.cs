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
                "fps-booking", "erasure/check-active", input);

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
                "fps-profile", "erasure", input);
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
                "fps-booking", "erasure", input);
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
                "fps-notification", "erasure", input);
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
                "fps-reporting", "erasure", input);
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
