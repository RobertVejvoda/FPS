namespace FPS.Customer.Domain;

public enum TenantAdminType { SsoMapped, Local }

public sealed record TenantAdminRecord(
    string TenantId,
    // Pseudonymised: hashed SSO subject or a stable local-account marker.
    // Raw subjects are never stored.
    string SubjectHash,
    TenantAdminType AdminType,
    string CreatedByHash,
    DateTimeOffset CreatedAt,
    string? AuditNote,
    bool IsActive);
