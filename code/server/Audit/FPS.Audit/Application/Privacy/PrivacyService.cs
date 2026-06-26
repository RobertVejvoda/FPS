using System.Diagnostics;
using Dapr.Workflow;
using FPS.Audit.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application.Privacy;

public sealed class PrivacyService(
    IErasureRequestRepository requestRepository,
    IAuditRepository auditRepository,
    IErasureWorkflowClient workflowClient,
    ILogger<PrivacyService> logger)
{
    public async Task<ErasureRequest> CreateErasureRequestAsync(
        string tenantId,
        string targetUserId,
        string requesterUserId,
        string legalBasis,
        CancellationToken cancellationToken = default)
    {
        var targetActorHash = Pseudonymiser.Hash(targetUserId)!;
        var requesterActorHash = Pseudonymiser.Hash(requesterUserId) ?? string.Empty;
        var traceId = Activity.Current?.TraceId.ToString();

        var request = new ErasureRequest
        {
            TenantId = tenantId,
            TargetActorHash = targetActorHash,
            RequestedByActorHash = requesterActorHash,
            LegalBasis = legalBasis,
            Status = ErasureStatus.Pending,
            TraceId = traceId,
        };

        await requestRepository.SaveAsync(request, cancellationToken);

        await RecordAuditEventAsync(
            tenantId, request.ErasureRequestId, targetActorHash, requesterActorHash,
            "privacy.erasureRequested", "erasureRequest", "accepted", legalBasis, traceId, cancellationToken);

        logger.LogInformation(
            "Erasure request created. ErasureRequestId={ErasureRequestId} TenantId={TenantId} LegalBasis={LegalBasis}",
            request.ErasureRequestId, tenantId, legalBasis);

        var workflowInput = new ErasureWorkflowInput(
            request.ErasureRequestId, tenantId, targetActorHash, requesterActorHash, legalBasis,
            TargetUserId: targetUserId);

        await workflowClient.ScheduleAsync(request.ErasureRequestId, workflowInput);

        await requestRepository.UpdateStatusAsync(
            request.ErasureRequestId, tenantId, ErasureStatus.InProgress,
            cancellationToken: cancellationToken);

        return request;
    }

    public async Task<ErasureStatusResponse?> GetStatusAsync(
        string erasureRequestId, string tenantId, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetAsync(erasureRequestId, tenantId, cancellationToken);
        if (request is null) return null;

        if (request.Status == ErasureStatus.InProgress)
        {
            try
            {
                var state = await workflowClient.GetStateAsync(erasureRequestId);

                if (state.RuntimeStatus == WorkflowRuntimeStatus.Completed)
                {
                    var output = state.ReadOutputAs<ErasureWorkflowOutput>();
                    if (output is not null)
                    {
                        var completedAt = DateTime.UtcNow;
                        await requestRepository.UpdateStatusAsync(
                            erasureRequestId, tenantId, output.Status,
                            output.ServiceResults, output.BlockReason, completedAt, cancellationToken);

                        await RecordAuditEventAsync(
                            tenantId, erasureRequestId, request.TargetActorHash, request.RequestedByActorHash,
                            output.Status == ErasureStatus.Blocked ? "privacy.erasureRejected" : "privacy.erasureCompleted",
                            "erasureRequest", output.Status, null, request.TraceId, cancellationToken);

                        request = await requestRepository.GetAsync(erasureRequestId, tenantId, cancellationToken)
                                  ?? request;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not sync workflow status. ErasureRequestId={ErasureRequestId}", erasureRequestId);
            }
        }

        return new ErasureStatusResponse(
            request.ErasureRequestId, request.TenantId, request.TargetActorHash,
            request.RequestedByActorHash, request.LegalBasis, request.RequestedAt,
            request.Status, request.CompletedAt, request.ServiceResults, request.BlockReason);
    }

    private async Task RecordAuditEventAsync(
        string tenantId, string erasureRequestId,
        string targetActorHash, string requesterActorHash,
        string eventType, string entityType, string result,
        string? legalBasis, string? traceId,
        CancellationToken cancellationToken)
    {
        var sourceEventId = $"{eventType}:{erasureRequestId}";
        if (await auditRepository.ExistsAsync(sourceEventId, tenantId, cancellationToken))
            return;

        var record = new AuditRecord
        {
            AuditRecordId = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            EventType = eventType,
            EventVersion = 1,
            OccurredAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow,
            TenantId = tenantId,
            CorrelationId = erasureRequestId,
            ActorType = "admin",
            ActorHash = requesterActorHash,
            Source = "privacy",
            EntityType = entityType,
            EntityId = erasureRequestId,
            Payload = new System.Text.Json.Nodes.JsonObject
            {
                ["erasureRequestId"] = erasureRequestId,
                ["targetActorHash"] = targetActorHash,
            },
            Action = eventType,
            Result = result,
            ReasonCode = legalBasis,
            Summary = BusinessActivityMapper.ToSummary(eventType, entityType, result, legalBasis),
            TraceId = traceId,
            ProcessingTraceId = Activity.Current?.TraceId.ToString(),
        };

        await auditRepository.AppendAsync(record, cancellationToken);
    }
}
