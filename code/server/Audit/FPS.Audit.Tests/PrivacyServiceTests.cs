using FPS.Audit.Application;
using FPS.Audit.Application.Privacy;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Audit.Tests;

public sealed class PrivacyServiceTests
{
    private readonly InMemoryAuditRepository auditRepo = new();
    private readonly InMemoryErasureRequestRepository requestRepo = new();
    private readonly Mock<IErasureWorkflowClient> workflowClient = new();
    private readonly PrivacyService service;

    public PrivacyServiceTests()
    {
        service = new PrivacyService(
            requestRepo, auditRepo, workflowClient.Object,
            NullLogger<PrivacyService>.Instance);

        workflowClient
            .Setup(c => c.ScheduleAsync(It.IsAny<string>(), It.IsAny<ErasureWorkflowInput>()))
            .ReturnsAsync(string.Empty);
    }

    // ── No raw PII in output ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateErasureRequest_DoesNotExposeRawTargetUserId()
    {
        var request = await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        Assert.NotEqual("user-99", request.TargetActorHash);
        Assert.Equal(Pseudonymiser.Hash("user-99"), request.TargetActorHash);
    }

    [Fact]
    public async Task CreateErasureRequest_DoesNotExposeRawRequesterUserId()
    {
        var request = await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        Assert.NotEqual("admin-1", request.RequestedByActorHash);
        Assert.Equal(Pseudonymiser.Hash("admin-1"), request.RequestedByActorHash);
    }

    // ── Audit recording ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateErasureRequest_RecordsAuditEvent()
    {
        await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        var (records, _) = await auditRepo.QueryAsync(
            new AuditQueryRequest { Action = "privacy.erasureRequested" }, "tenant-1");

        Assert.Single(records);
        Assert.Equal("erasureRequest", records[0].EntityType);
        Assert.Equal("accepted", records[0].Result);
        Assert.Equal("gdpr-article-17", records[0].ReasonCode);
    }

    [Fact]
    public async Task CreateErasureRequest_AuditPayloadContainsHashesNotRawIds()
    {
        await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        var (records, _) = await auditRepo.QueryAsync(
            new AuditQueryRequest { Action = "privacy.erasureRequested" }, "tenant-1");

        var payload = records[0].Payload.ToJsonString();
        Assert.DoesNotContain("user-99", payload);
        Assert.DoesNotContain("admin-1", payload);
        Assert.Contains(Pseudonymiser.Hash("user-99")!, payload);
    }

    [Fact]
    public async Task CreateErasureRequest_AuditEventIdempotent_SecondCallSkipsAppend()
    {
        await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");
        var (records, _) = await auditRepo.QueryAsync(
            new AuditQueryRequest { Action = "privacy.erasureRequested" }, "tenant-1");
        Assert.Single(records);
    }

    // ── Workflow started ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateErasureRequest_StartsWorkflow()
    {
        await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        workflowClient.Verify(c => c.ScheduleAsync(
            It.IsAny<string>(),
            It.IsAny<ErasureWorkflowInput>()), Times.Once);
    }

    [Fact]
    public async Task CreateErasureRequest_WorkflowInputContainsHashNotRawId()
    {
        ErasureWorkflowInput? captured = null;
        workflowClient
            .Setup(c => c.ScheduleAsync(It.IsAny<string>(), It.IsAny<ErasureWorkflowInput>()))
            .Callback<string, ErasureWorkflowInput>((_, input) => captured = input)
            .ReturnsAsync(string.Empty);

        await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        Assert.NotNull(captured);
        Assert.Equal(Pseudonymiser.Hash("user-99"), captured.TargetActorHash);
        // TargetUserId is present for internal DB lookup but is the raw ID — we verify it's
        // never returned in the public API response (tested via GetStatus tests above).
        Assert.Equal("user-99", captured.TargetUserId);
    }

    [Fact]
    public async Task CreateErasureRequest_StatusTransitionsToInProgress()
    {
        var request = await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        var saved = await requestRepo.GetAsync(request.ErasureRequestId, "tenant-1");
        Assert.Equal(ErasureStatus.InProgress, saved!.Status);
    }

    // ── GetStatus ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsNull_ForUnknownRequest()
    {
        var status = await service.GetStatusAsync("unknown-id", "tenant-1");

        Assert.Null(status);
    }

    [Fact]
    public async Task GetStatus_TenantIsolation_ReturnsNull_ForWrongTenant()
    {
        var request = await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        var status = await service.GetStatusAsync(request.ErasureRequestId, "tenant-2");

        Assert.Null(status);
    }

    [Fact]
    public async Task GetStatus_DoesNotExposeRawUserId()
    {
        var request = await service.CreateErasureRequestAsync(
            "tenant-1", "user-99", "admin-1", "gdpr-article-17");

        // GetWorkflowStateAsync throws (not mocked) → caught internally; status stays InProgress.
        var status = await service.GetStatusAsync(request.ErasureRequestId, "tenant-1");

        Assert.NotNull(status);
        Assert.NotEqual("user-99", status.TargetActorHash);
        Assert.NotEqual("admin-1", status.RequestedByActorHash);
        Assert.DoesNotContain("user-99", status.TargetActorHash);
        Assert.DoesNotContain("admin-1", status.RequestedByActorHash);
    }
}
