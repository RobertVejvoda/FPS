using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace FPS.Profile.Application;

public enum HrImportRowStatus { Created, Updated, Unchanged, Rejected }

public sealed record HrImportRow(
    int LineNumber,
    string ExternalSubject,
    HrImportRowStatus Status,
    string? Reason);

public sealed record HrImportPreview(
    IReadOnlyList<HrImportRow> Rows,
    int Created,
    int Updated,
    int Unchanged,
    int Rejected);

public sealed record HrImportCommitResult(
    int Applied,
    int Rejected,
    IReadOnlyList<string> Errors);

public sealed class HrImportService(
    IProfileRepository profileRepository,
    IDeactivatedUserStore deactivatedUserStore,
    ICurrentUser currentUser,
    ILogger<HrImportService> logger)
{
    private static readonly string[] EmployeeColumns =
    [
        "external_subject", "display_name", "email", "roles", "home_location",
        "preferred_zone", "parking_eligible", "has_company_car",
        "accessibility_eligible", "reserved_space_eligible", "active"
    ];

    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "hr_manager", "admin", "report_viewer", "auditor" };

    private static readonly HashSet<string> ForbiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "secret", "token", "credential", "ssn",
        "national_id", "salary", "employee_id", "manager_notes", "department"
    };

    public async Task<(HrImportPreview? preview, string? error)> PreviewAsync(
        string tenantId, Stream csvStream, CancellationToken ct)
    {
        var (rows, parseError) = await ParseCsvAsync(tenantId, csvStream, dryRun: true, ct);
        if (parseError is not null) return (null, parseError);

        var result = BuildPreview(rows);
        logger.LogInformation(
            "HR import preview: tenantId={TenantId} actor={Actor} created={Created} updated={Updated} unchanged={Unchanged} rejected={Rejected}",
            tenantId, currentUser.UserId, result.Created, result.Updated, result.Unchanged, result.Rejected);

        return (result, null);
    }

    public async Task<(HrImportCommitResult? result, string? error)> CommitAsync(
        string tenantId, Stream csvStream, CancellationToken ct)
    {
        var (rows, parseError) = await ParseCsvAsync(tenantId, csvStream, dryRun: false, ct);
        if (parseError is not null) return (null, parseError);

        var errors = rows
            .Where(r => r.Status == HrImportRowStatus.Rejected)
            .Select(r => $"Line {r.LineNumber} ({r.ExternalSubject}): {r.Reason}")
            .ToList();

        var applied = rows.Count(r => r.Status is HrImportRowStatus.Created or HrImportRowStatus.Updated);

        logger.LogInformation(
            "HR import committed: tenantId={TenantId} actor={Actor} applied={Applied} rejected={Rejected}",
            tenantId, currentUser.UserId, applied, errors.Count);

        return (new HrImportCommitResult(applied, errors.Count, errors), null);
    }

    private async Task<(List<HrImportRow> rows, string? error)> ParseCsvAsync(
        string tenantId, Stream csvStream, bool dryRun, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream);
        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || string.IsNullOrEmpty(trimmed)) continue;
            lines.Add(trimmed);
        }

        if (lines.Count == 0) return ([], "CSV is empty or contains only comments.");

        var header = lines[0].Split(',');
        foreach (var col in header)
        {
            var c = col.Trim();
            if (ForbiddenColumns.Contains(c))
                return ([], $"Forbidden column '{c}' — do not include secrets or personal data.");
            if (!EmployeeColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                return ([], $"Unknown column '{c}' — only the documented column set is allowed.");
        }

        var colIndex = BuildColumnIndex(header);
        if (!colIndex.ContainsKey("external_subject"))
            return ([], "Missing required column 'external_subject'.");

        var rows = new List<HrImportRow>();
        var seenSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Count; i++)
        {
            var lineNo = i + 1;
            var fields = SplitCsvLine(lines[i]);
            if (fields.Length < header.Length)
            {
                rows.Add(new HrImportRow(lineNo, "", HrImportRowStatus.Rejected, "Too few columns."));
                continue;
            }

            var subject = GetField(fields, colIndex, "external_subject").Trim();
            if (string.IsNullOrEmpty(subject))
            {
                rows.Add(new HrImportRow(lineNo, subject, HrImportRowStatus.Rejected, "external_subject is required."));
                continue;
            }

            if (!seenSubjects.Add(subject))
            {
                rows.Add(new HrImportRow(lineNo, subject, HrImportRowStatus.Rejected, "Duplicate external_subject."));
                continue;
            }

            var roleString = GetField(fields, colIndex, "roles");
            var roles = roleString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(r => r.Trim())
                                  .ToList();
            var invalidRole = roles.FirstOrDefault(r => !ValidRoles.Contains(r));
            if (invalidRole is not null)
            {
                rows.Add(new HrImportRow(lineNo, subject, HrImportRowStatus.Rejected, $"Unknown role '{invalidRole}'."));
                continue;
            }

            var parkingEligible = ParseBool(GetField(fields, colIndex, "parking_eligible"), out var peErr);
            var hasCompanyCar = ParseBool(GetField(fields, colIndex, "has_company_car"), out var hccErr);
            var accessibilityEligible = ParseBool(GetField(fields, colIndex, "accessibility_eligible"), out var aeErr);
            var reservedSpaceEligible = ParseBool(GetField(fields, colIndex, "reserved_space_eligible"), out var rseErr);
            var isActive = ParseBool(GetField(fields, colIndex, "active"), out var actErr);

            var boolError = peErr ?? hccErr ?? aeErr ?? rseErr ?? actErr;
            if (boolError is not null)
            {
                rows.Add(new HrImportRow(lineNo, subject, HrImportRowStatus.Rejected, boolError));
                continue;
            }

            var homeLocation = GetField(fields, colIndex, "home_location").Trim();
            var subjectHash = EmployeeBootstrapService.Hash(subject);
            var existing = await profileRepository.GetAsync(tenantId, subjectHash, ct);

            HrImportRowStatus status;
            if (existing is null)
            {
                status = HrImportRowStatus.Created;
                if (!dryRun)
                {
                    var req = new BootstrapEmployeeRequest(
                        subject, null, isActive, roles,
                        GetField(fields, colIndex, "email").Trim().NullIfEmpty(),
                        homeLocation.NullIfEmpty(), parkingEligible, hasCompanyCar,
                        accessibilityEligible, reservedSpaceEligible, "hr-import");
                    await new EmployeeBootstrapService(profileRepository, deactivatedUserStore)
                        .RegisterAsync(tenantId, req, ct);
                }
            }
            else
            {
                var updateReq = new UpdateEmployeeRequest(
                    isActive, roles,
                    GetField(fields, colIndex, "email").Trim().NullIfEmpty(),
                    homeLocation.NullIfEmpty(),
                    parkingEligible, hasCompanyCar, accessibilityEligible, reservedSpaceEligible);

                status = IsUnchanged(existing, updateReq) ? HrImportRowStatus.Unchanged : HrImportRowStatus.Updated;
                if (!dryRun && status == HrImportRowStatus.Updated)
                {
                    await new EmployeeBootstrapService(profileRepository, deactivatedUserStore)
                        .UpdateAsync(tenantId, subjectHash, updateReq, ct);
                }
            }

            rows.Add(new HrImportRow(lineNo, subject, status, null));
        }

        return (rows, null);
    }

    private static HrImportPreview BuildPreview(List<HrImportRow> rows) =>
        new(rows,
            rows.Count(r => r.Status == HrImportRowStatus.Created),
            rows.Count(r => r.Status == HrImportRowStatus.Updated),
            rows.Count(r => r.Status == HrImportRowStatus.Unchanged),
            rows.Count(r => r.Status == HrImportRowStatus.Rejected));

    private static bool IsUnchanged(UserProfile existing, UpdateEmployeeRequest req) =>
        existing.IsActive == req.IsActive &&
        existing.ParkingEligible == req.ParkingEligible &&
        existing.HasCompanyCar == req.HasCompanyCar &&
        existing.AccessibilityEligible == req.AccessibilityEligible &&
        existing.ReservedSpaceEligible == req.ReservedSpaceEligible &&
        existing.HomeLocationId == req.HomeLocationId;

    private static Dictionary<string, int> BuildColumnIndex(string[] header)
    {
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
            idx[header[i].Trim()] = i;
        return idx;
    }

    private static string GetField(string[] fields, Dictionary<string, int> colIndex, string col) =>
        colIndex.TryGetValue(col, out var i) && i < fields.Length ? fields[i].Trim() : "";

    private static string[] SplitCsvLine(string line) => line.Split(',');

    private static bool ParseBool(string value, out string? error)
    {
        if (string.IsNullOrEmpty(value)) { error = null; return false; }
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) { error = null; return true; }
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) { error = null; return false; }
        error = $"Boolean field has non-boolean value '{value}'.";
        return false;
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
