using FPS.Customer.Domain;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Profile;

namespace FPS.Customer.Application;

file static class KnownFpsRoles
{
    internal static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "hr_manager", "admin", "report_viewer" };
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
    string FactSource);

public sealed record UpdateEmployeeRequest(
    bool IsActive,
    IReadOnlyList<string> FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible);

public sealed record ImportSummary(int Accepted, int Rejected, IReadOnlyList<string> Errors);

public sealed class EmployeeBootstrapService(
    IEmployeeBootstrapRepository repository,
    ITenantRepository tenantRepository,
    IProfileBootstrapSink profileSink,
    IDeactivatedUserStore deactivatedUserStore)
{
    public async Task<(EmployeeBootstrapRecord? record, string? error)> RegisterAsync(
        string tenantId, BootstrapEmployeeRequest request, string actorHash, CancellationToken ct)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null) return (null, validationError);

        var tenant = await tenantRepository.GetAsync(tenantId, ct);
        if (tenant is null) return (null, "Tenant not found.");
        if (tenant.LifecycleState == TenantLifecycleState.Archived)
            return (null, "Cannot bootstrap employees for an archived tenant.");

        var subjectHash = Hash(request.ExternalSubject);

        if (await repository.SubjectExistsAsync(tenantId, subjectHash, ct))
            return (null, "An employee with this external subject is already registered for this tenant.");

        if (request.EmployeeId is not null &&
            await repository.EmployeeIdExistsAsync(tenantId, request.EmployeeId, ct))
            return (null, $"Employee ID '{request.EmployeeId}' is already registered for this tenant.");

        var record = new EmployeeBootstrapRecord
        {
            TenantId = tenantId,
            ExternalSubjectHash = subjectHash,
            EmployeeId = request.EmployeeId,
            IsActive = request.IsActive,
            FpsRoles = request.FpsRoles,
            NotificationAddress = request.NotificationAddress,
            HomeLocationId = request.HomeLocationId,
            ParkingEligible = request.ParkingEligible,
            HasCompanyCar = request.HasCompanyCar,
            AccessibilityEligible = request.AccessibilityEligible,
            ReservedSpaceEligible = request.ReservedSpaceEligible,
            FactSource = request.FactSource,
            RecordedByHash = actorHash,
            RecordedAt = DateTimeOffset.UtcNow,
        };

        await repository.SaveAsync(record, ct);
        await SyncToProfileAndDeactivatedStore(record, ct);
        return (record, null);
    }

    public async Task<string?> UpdateAsync(
        string tenantId, string externalSubject, UpdateEmployeeRequest request, string actorHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalSubject)) return "External subject is required.";

        var roleError = ValidateRoles(request.FpsRoles);
        if (roleError is not null) return roleError;

        var subjectHash = Hash(externalSubject);
        var record = await repository.GetAsync(tenantId, subjectHash, ct);
        if (record is null) return "Employee not found.";

        record.IsActive = request.IsActive;
        record.FpsRoles = request.FpsRoles;
        record.NotificationAddress = request.NotificationAddress;
        record.HomeLocationId = request.HomeLocationId;
        record.ParkingEligible = request.ParkingEligible;
        record.HasCompanyCar = request.HasCompanyCar;
        record.AccessibilityEligible = request.AccessibilityEligible;
        record.ReservedSpaceEligible = request.ReservedSpaceEligible;
        record.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.SaveAsync(record, ct);
        await SyncToProfileAndDeactivatedStore(record, ct);
        return null;
    }

    public async Task<string?> DeactivateAsync(string tenantId, string externalSubject, string actorHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalSubject)) return "External subject is required.";
        var subjectHash = Hash(externalSubject);
        var record = await repository.GetAsync(tenantId, subjectHash, ct);
        if (record is null) return "Employee not found.";

        record.IsActive = false;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveAsync(record, ct);
        await SyncToProfileAndDeactivatedStore(record, ct);
        return null;
    }

    // Validates all rows first; commits only valid rows (no partial writes).
    public async Task<ImportSummary> ImportAsync(
        string tenantId, IReadOnlyList<BootstrapEmployeeRequest> requests, string actorHash, CancellationToken ct)
    {
        var errors = new List<string>();
        var valid = new List<(int index, BootstrapEmployeeRequest req, string subjectHash)>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmployeeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: validate all rows, collect duplicates within the batch.
        for (var i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            var rowLabel = $"Row {i + 1}";
            var rowError = ValidateRequest(req);
            if (rowError is not null) { errors.Add($"{rowLabel}: {rowError}"); continue; }

            var hash = Hash(req.ExternalSubject);

            if (seenHashes.Contains(hash))
            { errors.Add($"{rowLabel}: duplicate external subject within this batch."); continue; }

            if (await repository.SubjectExistsAsync(tenantId, hash, ct))
            { errors.Add($"{rowLabel}: subject already registered for this tenant."); continue; }

            if (req.EmployeeId is not null)
            {
                if (seenEmployeeIds.Contains(req.EmployeeId))
                { errors.Add($"{rowLabel}: duplicate employee ID within this batch."); continue; }
                if (await repository.EmployeeIdExistsAsync(tenantId, req.EmployeeId, ct))
                { errors.Add($"{rowLabel}: employee ID '{req.EmployeeId}' already registered."); continue; }
                seenEmployeeIds.Add(req.EmployeeId);
            }

            seenHashes.Add(hash);
            valid.Add((i, req, hash));
        }

        // Phase 2: commit only valid rows.
        foreach (var (_, req, hash) in valid)
        {
            var record = new EmployeeBootstrapRecord
            {
                TenantId = tenantId,
                ExternalSubjectHash = hash,
                EmployeeId = req.EmployeeId,
                IsActive = req.IsActive,
                FpsRoles = req.FpsRoles,
                NotificationAddress = req.NotificationAddress,
                HomeLocationId = req.HomeLocationId,
                ParkingEligible = req.ParkingEligible,
                HasCompanyCar = req.HasCompanyCar,
                AccessibilityEligible = req.AccessibilityEligible,
                ReservedSpaceEligible = req.ReservedSpaceEligible,
                FactSource = "file-import",
                RecordedByHash = actorHash,
                RecordedAt = DateTimeOffset.UtcNow,
            };
            await repository.SaveAsync(record, ct);
            await SyncToProfileAndDeactivatedStore(record, ct);
        }

        return new ImportSummary(valid.Count, errors.Count, errors);
    }

    public async Task<BootstrapSummary> GetSummaryAsync(string tenantId, CancellationToken ct)
    {
        var all = await repository.ListAsync(tenantId, ct);
        return new BootstrapSummary(
            all.Count,
            all.Count(r => r.IsActive),
            all.Count(r => !r.IsActive),
            all.Count(r => r.ParkingEligible && r.IsActive));
    }

    public async Task<EmployeeBootstrapRecord?> GetAsync(string tenantId, string externalSubject, CancellationToken ct) =>
        await repository.GetAsync(tenantId, Hash(externalSubject), ct);

    private async Task SyncToProfileAndDeactivatedStore(EmployeeBootstrapRecord record, CancellationToken ct)
    {
        // Write eligibility facts into Profile so Booking snapshot reads reflect bootstrap data.
        await profileSink.UpsertAsync(
            record.TenantId, record.ExternalSubjectHash, record.IsActive,
            record.ParkingEligible, record.HasCompanyCar,
            record.AccessibilityEligible, record.ReservedSpaceEligible,
            record.FactSource, ct);

        // Enforce inactive status via the deactivated user store so Booking rejects requests.
        if (!record.IsActive)
            deactivatedUserStore.Deactivate(record.TenantId, record.ExternalSubjectHash);
        else
            deactivatedUserStore.Reactivate(record.TenantId, record.ExternalSubjectHash);
    }

    private static string? ValidateRequest(BootstrapEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalSubject))
            return "ExternalSubject (stable subject) is required.";
        if (string.IsNullOrWhiteSpace(request.FactSource))
            return "FactSource is required.";
        return ValidateRoles(request.FpsRoles);
    }

    private static string? ValidateRoles(IReadOnlyList<string> roles)
    {
        var unknown = roles.Where(r => !KnownFpsRoles.All.Contains(r)).ToList();
        if (unknown.Count > 0)
            return $"Unknown FPS role(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", KnownFpsRoles.All)}.";
        return null;
    }

    public static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..32];
}

public sealed record BootstrapSummary(int Total, int Active, int Inactive, int ActiveAndEligible);
