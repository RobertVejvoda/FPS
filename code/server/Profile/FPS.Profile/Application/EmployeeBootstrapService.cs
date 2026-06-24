using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;

namespace FPS.Profile.Application;

file static class KnownFpsRoles
{
    internal static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "hr_manager", "admin", "report_viewer", "auditor" };
}

public sealed record BootstrapEmployeeRequest(
    string ExternalSubject,
    string? EmployeeId,
    bool IsActive,
    IReadOnlyList<string> FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    string FactSource,
    string? DisplayName = null);

public sealed record UpdateEmployeeRequest(
    bool IsActive,
    IReadOnlyList<string> FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    string? DisplayName = null);

public sealed record ImportSummary(int Accepted, int Rejected, IReadOnlyList<string> Errors);
public sealed record BootstrapSummary(int Total, int Active, int Inactive, int ActiveAndEligible);

public sealed class EmployeeBootstrapService(
    IProfileRepository profileRepository,
    IDeactivatedUserStore deactivatedUserStore)
{
    public async Task<(UserProfile? profile, string? error)> RegisterAsync(
        string tenantId, BootstrapEmployeeRequest request, CancellationToken ct)
    {
        var err = ValidateRequest(request);
        if (err is not null) return (null, err);

        // Use ExternalSubject directly as the profile key — the same key that
        // ProfileSnapshotController and Booking use (raw JWT sub UUID). Storing
        // under a hash broke eligibility lookups for HR-imported employees.
        var userId = request.ExternalSubject;

        var existing = await profileRepository.GetAsync(tenantId, userId, ct);
        if (existing is not null)
            return (null, "An employee with this external subject is already registered for this tenant.");

        if (request.EmployeeId is not null &&
            await profileRepository.EmployeeIdExistsAsync(tenantId, request.EmployeeId, ct))
            return (null, $"Employee ID '{request.EmployeeId}' is already registered for this tenant.");

        var profile = BuildProfile(tenantId, userId, request);
        await profileRepository.SaveAsync(profile, ct);
        SyncDeactivatedStore(tenantId, userId, profile.IsActive);
        return (profile, null);
    }

    // Scoped HR/admin update for allocation-impacting eligibility flags only.
    // Issue #481: separated from the full UpdateAsync so the HR Operations
    // requestor drawer can flip company-car / accessibility without
    // accidentally clobbering home location, roles, or notification address.
    // A null flag leaves the existing value untouched.
    public async Task<(UserProfile? profile, string? error)> UpdateEligibilityAsync(
        string tenantId, string subjectHash,
        bool? hasCompanyCar, bool? accessibilityEligible, CancellationToken ct)
    {
        if (hasCompanyCar is null && accessibilityEligible is null)
            return (null, "At least one eligibility field must be specified.");

        var existing = await profileRepository.GetAsync(tenantId, subjectHash, ct);
        if (existing is null) return (null, "Employee not found.");

        var updated = new UserProfile
        {
            TenantId = existing.TenantId,
            UserId = existing.UserId,
            EmployeeId = existing.EmployeeId,
            DisplayName = existing.DisplayName,
            Status = existing.Status,
            FpsRoles = existing.FpsRoles,
            NotificationAddress = existing.NotificationAddress,
            HomeLocationId = existing.HomeLocationId,
            ParkingEligible = existing.ParkingEligible,
            HasCompanyCar = hasCompanyCar ?? existing.HasCompanyCar,
            AccessibilityEligible = accessibilityEligible ?? existing.AccessibilityEligible,
            ReservedSpaceEligible = existing.ReservedSpaceEligible,
            Vehicles = existing.Vehicles,
            SnapshotVersion = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            FactSource = existing.FactSource,
        };

        await profileRepository.SaveAsync(updated, ct);
        return (updated, null);
    }

    public async Task<string?> UpdateAsync(
        string tenantId, string subjectHash, UpdateEmployeeRequest request, CancellationToken ct)
    {
        var roleErr = ValidateRoles(request.FpsRoles);
        if (roleErr is not null) return roleErr;

        var existing = await profileRepository.GetAsync(tenantId, subjectHash, ct);
        if (existing is null) return "Employee not found.";

        var updated = new UserProfile
        {
            TenantId = tenantId,
            UserId = subjectHash,
            EmployeeId = existing.EmployeeId,
            DisplayName = request.DisplayName ?? existing.DisplayName,
            Status = request.IsActive ? ProfileStatus.Active : ProfileStatus.Inactive,
            FpsRoles = request.FpsRoles,
            NotificationAddress = request.NotificationAddress,
            HomeLocationId = request.HomeLocationId,
            ParkingEligible = request.ParkingEligible,
            HasCompanyCar = request.HasCompanyCar,
            AccessibilityEligible = request.AccessibilityEligible,
            ReservedSpaceEligible = request.ReservedSpaceEligible,
            Vehicles = existing.Vehicles,
            SnapshotVersion = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            FactSource = existing.FactSource,
        };

        await profileRepository.SaveAsync(updated, ct);
        SyncDeactivatedStore(tenantId, subjectHash, updated.IsActive);
        return null;
    }

    public async Task<string?> DeactivateAsync(string tenantId, string externalSubject, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalSubject)) return "External subject is required.";
        var existing = await profileRepository.GetAsync(tenantId, externalSubject, ct);
        if (existing is null) return "Employee not found.";

        var updated = new UserProfile
        {
            TenantId = tenantId, UserId = externalSubject,
            Status = ProfileStatus.Inactive,
            EmployeeId = existing.EmployeeId,
            DisplayName = existing.DisplayName,
            FpsRoles = existing.FpsRoles,
            NotificationAddress = existing.NotificationAddress,
            HomeLocationId = existing.HomeLocationId,
            ParkingEligible = existing.ParkingEligible,
            HasCompanyCar = existing.HasCompanyCar,
            AccessibilityEligible = existing.AccessibilityEligible,
            ReservedSpaceEligible = existing.ReservedSpaceEligible,
            Vehicles = existing.Vehicles,
            SnapshotVersion = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            FactSource = existing.FactSource,
        };

        await profileRepository.SaveAsync(updated, ct);
        SyncDeactivatedStore(tenantId, externalSubject, false);
        return null;
    }

    // Atomic import: validate ALL rows first, commit NONE if any row is invalid.
    public async Task<ImportSummary> ImportAsync(
        string tenantId, IReadOnlyList<BootstrapEmployeeRequest> requests, CancellationToken ct)
    {
        var errors = new List<string>();
        var valid = new List<(BootstrapEmployeeRequest req, string userId)>();
        var seenUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmployeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: validate all rows — no saves yet.
        for (var i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            var label = $"Row {i + 1}";
            var err = ValidateRequest(req);
            if (err is not null) { errors.Add($"{label}: {err}"); continue; }

            var userId = req.ExternalSubject;
            if (seenUserIds.Contains(userId))
            { errors.Add($"{label}: duplicate external subject within this batch."); continue; }
            if (await profileRepository.GetAsync(tenantId, userId, ct) is not null)
            { errors.Add($"{label}: subject already registered for this tenant."); continue; }

            if (req.EmployeeId is not null)
            {
                if (seenEmployeeIds.Contains(req.EmployeeId))
                { errors.Add($"{label}: duplicate employee ID within this batch."); continue; }
                if (await profileRepository.EmployeeIdExistsAsync(tenantId, req.EmployeeId, ct))
                { errors.Add($"{label}: employee ID '{req.EmployeeId}' already registered."); continue; }
                seenEmployeeIds.Add(req.EmployeeId);
            }

            seenUserIds.Add(userId);
            valid.Add((req, userId));
        }

        // Phase 2: commit ONLY when the entire batch is valid.
        if (errors.Count > 0) return new ImportSummary(0, errors.Count, errors);

        foreach (var (req, userId) in valid)
        {
            var profile = BuildProfile(tenantId, userId, req);
            await profileRepository.SaveAsync(profile, ct);
            SyncDeactivatedStore(tenantId, userId, profile.IsActive);
        }

        return new ImportSummary(valid.Count, 0, []);
    }

    public async Task<UserProfile?> GetAsync(string tenantId, string externalSubject, CancellationToken ct) =>
        await profileRepository.GetAsync(tenantId, externalSubject, ct);

    public async Task<UserProfile?> GetByHashAsync(string tenantId, string subjectHash, CancellationToken ct) =>
        await profileRepository.GetAsync(tenantId, subjectHash, ct);

    private static UserProfile BuildProfile(string tenantId, string subjectHash, BootstrapEmployeeRequest req) =>
        new()
        {
            TenantId = tenantId,
            UserId = subjectHash,
            EmployeeId = req.EmployeeId,
            DisplayName = req.DisplayName,
            Status = req.IsActive ? ProfileStatus.Active : ProfileStatus.Inactive,
            FpsRoles = req.FpsRoles,
            NotificationAddress = req.NotificationAddress,
            HomeLocationId = req.HomeLocationId,
            ParkingEligible = req.ParkingEligible,
            HasCompanyCar = req.HasCompanyCar,
            AccessibilityEligible = req.AccessibilityEligible,
            ReservedSpaceEligible = req.ReservedSpaceEligible,
            Vehicles = [],
            SnapshotVersion = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow,
            FactSource = req.FactSource,
        };

    private void SyncDeactivatedStore(string tenantId, string subjectHash, bool isActive)
    {
        if (!isActive) deactivatedUserStore.Deactivate(tenantId, subjectHash);
        else deactivatedUserStore.Reactivate(tenantId, subjectHash);
    }

    private static string? ValidateRequest(BootstrapEmployeeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ExternalSubject)) return "ExternalSubject is required.";
        if (string.IsNullOrWhiteSpace(req.FactSource)) return "FactSource is required.";
        return ValidateRoles(req.FpsRoles);
    }

    private static string? ValidateRoles(IReadOnlyList<string> roles)
    {
        var unknown = roles.Where(r => !KnownFpsRoles.All.Contains(r)).ToList();
        return unknown.Count > 0
            ? $"Unknown FPS role(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", KnownFpsRoles.All)}."
            : null;
    }

    public static bool IsKnownRole(string role) => KnownFpsRoles.All.Contains(role);

    public static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..32];
}
