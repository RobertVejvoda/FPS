namespace FPS.Customer.Domain;

/// <summary>Lifecycle of a prospect's request for a tenant (distinct from a provisioned tenant's lifecycle).</summary>
public enum TenantRequestStatus
{
    Requested,
    Approved,
    Rejected,
}

/// <summary>
/// PLAT004 — a request-based onboarding record. The in-product system of record for a prospect's
/// "Request a tenant" submission: keeps prospect PII inside the platform and ties into onboarding
/// triage. No tenant is provisioned on submission; an operator advances it later.
/// </summary>
public sealed record TenantRequest
{
    public required string RequestId { get; init; }
    public required string Company { get; init; }
    public required string PrimaryDomain { get; init; }
    public required string ContactEmail { get; init; }
    public string Message { get; init; } = string.Empty;
    public TenantRequestStatus Status { get; init; } = TenantRequestStatus.Requested;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public string? DecidedByHash { get; init; }
    public string? DecisionReason { get; init; }
}
