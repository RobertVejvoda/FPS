using FPS.Customer.Domain;

namespace FPS.Customer.Application;

// Known FPS roles — role mapping is tenant-scoped; arbitrary role strings are rejected.
file static class KnownFpsRoles
{
    internal static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "hr_manager", "admin", "report_viewer" };
}

public sealed record BootstrapEmployeeRequest(
    string ExternalSubject,        // raw subject — hashed by service before storage
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

public sealed record ImportSummary(int Accepted, int Rejected, IReadOnlyList<string> Errors);

public sealed class EmployeeBootstrapService(
    IEmployeeBootstrapRepository repository,
    ITenantRepository tenantRepository)
{
    public async Task<(EmployeeBootstrapRecord? record, string? error)> RegisterAsync(
        string tenantId, BootstrapEmployeeRequest request, string actorHash, CancellationToken ct)
    {
        var validationError = Validate(request);
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
        return (record, null);
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
        return null;
    }

    public async Task<ImportSummary> ImportAsync(
        string tenantId, IReadOnlyList<BootstrapEmployeeRequest> requests, string actorHash, CancellationToken ct)
    {
        var accepted = 0;
        var errors = new List<string>();

        foreach (var (req, i) in requests.Select((r, i) => (r, i)))
        {
            var (_, error) = await RegisterAsync(tenantId, req, actorHash, ct);
            if (error is null) accepted++;
            else errors.Add($"Row {i + 1}: {error}");
        }

        return new ImportSummary(accepted, errors.Count, errors);
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

    private static string? Validate(BootstrapEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalSubject))
            return "ExternalSubject (stable subject) is required.";
        if (request.FpsRoles.Any(r => !KnownFpsRoles.All.Contains(r)))
            return $"Unknown FPS role(s): {string.Join(", ", request.FpsRoles.Where(r => !KnownFpsRoles.All.Contains(r)))}. Allowed: {string.Join(", ", KnownFpsRoles.All)}.";
        if (string.IsNullOrWhiteSpace(request.FactSource))
            return "FactSource is required (sso-bootstrap, admin-entry, or file-import).";
        return null;
    }

    internal static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..32];
}

public sealed record BootstrapSummary(int Total, int Active, int Inactive, int ActiveAndEligible);
