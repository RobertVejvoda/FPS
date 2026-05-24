namespace FPS.Audit.Application.Privacy;

public sealed record ErasureRequest
{
    public string ErasureRequestId { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string TargetActorHash { get; init; } = string.Empty;
    public string RequestedByActorHash { get; init; } = string.Empty;
    public string LegalBasis { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = ErasureStatus.Pending;
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyList<ErasureServiceResult> ServiceResults { get; init; } = [];
    public string? BlockReason { get; init; }
    public string? TraceId { get; init; }
}

public sealed record ErasureServiceResult(
    string Service,
    string Treatment,
    int AffectedCount,
    string? Note = null);

public sealed record ErasureStatusResponse(
    string ErasureRequestId,
    string TenantId,
    string TargetActorHash,
    string RequestedByActorHash,
    string LegalBasis,
    DateTime RequestedAt,
    string Status,
    DateTime? CompletedAt,
    IReadOnlyList<ErasureServiceResult> ServiceResults,
    string? BlockReason);

public sealed record CreateErasureRequest(
    string TargetUserId,
    string LegalBasis)
{
    public static readonly IReadOnlySet<string> AllowedLegalBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gdpr-article-17",
        "gdpr-article-17-1-a",
        "gdpr-article-17-1-b",
        "gdpr-article-17-1-c",
        "gdpr-article-17-1-d",
        "ccpa-deletion-request",
        "contract-termination",
        "consent-withdrawn",
        "legal-obligation",
        "client-request",
    };

    public bool IsValidLegalBasis => AllowedLegalBases.Contains(LegalBasis);
};

// Input/output types for the Dapr Workflow
public sealed record ErasureWorkflowInput(
    string ErasureRequestId,
    string TenantId,
    string TargetActorHash,
    string RequestedByActorHash,
    string LegalBasis,
    // Internal only: used by service activities for DB lookup. Never logged or returned.
    string? TargetUserId = null);

public sealed record ErasureWorkflowOutput(
    string Status,
    IReadOnlyList<ErasureServiceResult> ServiceResults,
    string? BlockReason);

public sealed record ServiceErasureInput(
    string ErasureRequestId,
    string TenantId,
    string TargetActorHash,
    // Internal only: used by services that store raw user IDs for lookup.
    // Must never be logged or returned in API responses.
    string? TargetUserId = null);

public sealed record ErasureStepAuditInput(
    ErasureWorkflowInput WorkflowInput,
    ErasureServiceResult StepResult);

public static class ErasureStatus
{
    public const string Pending = "pending";
    public const string InProgress = "inProgress";
    public const string Blocked = "blocked";
    public const string Completed = "completed";
    public const string PartiallyCompleted = "partiallyCompleted";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}

public static class ErasureTreatment
{
    public const string Deleted = "deleted";
    public const string Anonymised = "anonymised";
    public const string Pseudonymised = "pseudonymised";
    public const string Retained = "retained";
    public const string Blocked = "blocked";
    public const string Failed = "failed";
    public const string NotApplicable = "notApplicable";
}
