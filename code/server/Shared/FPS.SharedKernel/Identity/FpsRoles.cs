namespace FPS.SharedKernel.Identity;

/// <summary>
/// Canonical FairSpot role names and the two authorization planes (PLAT001).
///
/// <para><b>Tenant plane</b> — roles scoped to the caller's own tenant. A tenant
/// <see cref="Admin"/> can administer only the tenant in its own token.</para>
///
/// <para><b>Platform plane</b> — roles prefixed <c>platform_</c> are cross-tenant
/// (FairSpot operators). They are honored <i>only</i> on a token issued by the
/// trusted platform issuer; <see cref="TenantClaimsTransformation"/> strips any
/// <c>platform_*</c> role from a customer-issuer token, and
/// <see cref="ConfiguredTenantRoleMapper"/> never maps a tenant's IdP groups to a
/// platform role. A customer tenant therefore can never mint a platform role.</para>
/// </summary>
public static class FpsRoles
{
    // ── Tenant plane (own tenant only) ────────────────────────────────────────
    public const string Admin = "admin";
    public const string HrManager = "hr_manager";
    public const string Employee = "employee";
    public const string Auditor = "auditor";
    public const string ReportViewer = "report_viewer";

    // ── Platform plane (cross-tenant; trusted platform issuer only) ────────────
    public const string PlatformPrefix = "platform_";
    public const string PlatformAdmin = "platform_admin";
    public const string PlatformOperator = "platform_operator";
    public const string PlatformAuditor = "platform_auditor";

    /// <summary>True when <paramref name="role"/> is a cross-tenant platform-plane role.</summary>
    public static bool IsPlatformRole(string? role) =>
        !string.IsNullOrEmpty(role) && role.StartsWith(PlatformPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="role"/> is an elevated tenant-plane role. Privileged roles
    /// are never granted implicitly from a raw token claim (PLAT001): they require an explicit
    /// per-tenant mapping or a seeded <c>Auth:TrustedRealmRoles</c> allowlist. <c>employee</c>
    /// is not privileged.
    /// </summary>
    public static bool IsPrivileged(string? role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, HrManager, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Auditor, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, ReportViewer, StringComparison.OrdinalIgnoreCase);
}
