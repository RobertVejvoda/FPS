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

// Internal: full row data preserved across classification and apply phases.
internal sealed record ClassifiedRow(
    int LineNumber,
    string ExternalSubject,
    string SubjectHash,
    HrImportRowStatus Status,
    string? Reason,
    BootstrapEmployeeRequest? CreateRequest,
    UpdateEmployeeRequest? UpdateRequest);

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

    private static readonly HashSet<string> ForbiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "secret", "token", "credential", "ssn",
        "national_id", "salary", "employee_id", "manager_notes", "department"
    };

    public async Task<(HrImportPreview? preview, string? error)> PreviewAsync(
        string tenantId, Stream csvStream, CancellationToken ct)
    {
        var (rows, parseError) = await ClassifyAllAsync(tenantId, csvStream, ct);
        if (parseError is not null) return (null, parseError);

        var preview = ToPreview(rows);
        logger.LogInformation(
            "HR import preview: tenantId={TenantId} actor={Actor} created={Created} updated={Updated} unchanged={Unchanged} rejected={Rejected}",
            tenantId, currentUser.UserId, preview.Created, preview.Updated, preview.Unchanged, preview.Rejected);

        return (preview, null);
    }

    public async Task<(HrImportCommitResult? result, string? error)> CommitAsync(
        string tenantId, Stream csvStream, CancellationToken ct)
    {
        // Phase 1: classify all rows — no DB writes.
        var (rows, parseError) = await ClassifyAllAsync(tenantId, csvStream, ct);
        if (parseError is not null) return (null, parseError);

        // Reject the entire commit if any row failed validation.
        var rejected = rows.Where(r => r.Status == HrImportRowStatus.Rejected).ToList();
        if (rejected.Count > 0)
        {
            var errors = rejected.Select(r => $"Line {r.LineNumber} ({r.ExternalSubject}): {r.Reason}").ToList();
            return (new HrImportCommitResult(0, rejected.Count, errors), null);
        }

        // Phase 2: apply all valid rows, surface service-level errors per row.
        var applyErrors = new List<string>();
        var applied = 0;
        var bootstrapService = new EmployeeBootstrapService(profileRepository, deactivatedUserStore);

        foreach (var row in rows)
        {
            if (row.Status == HrImportRowStatus.Unchanged) continue;

            string? serviceError;
            if (row.Status == HrImportRowStatus.Created && row.CreateRequest is not null)
            {
                var (_, err) = await bootstrapService.RegisterAsync(tenantId, row.CreateRequest, ct);
                serviceError = err;
            }
            else if (row.Status == HrImportRowStatus.Updated && row.UpdateRequest is not null)
            {
                serviceError = await bootstrapService.UpdateAsync(tenantId, row.SubjectHash, row.UpdateRequest, ct);
            }
            else continue;

            if (serviceError is not null)
                applyErrors.Add($"Line {row.LineNumber} ({row.ExternalSubject}): {serviceError}");
            else
                applied++;
        }

        // Structured log — not an Audit service event. A follow-up slice should
        // publish to the Dapr fps-pubsub audit topic for full audit trail coverage.
        logger.LogInformation(
            "HR import committed: tenantId={TenantId} actor={Actor} applied={Applied} applyErrors={ApplyErrors}",
            tenantId, currentUser.UserId, applied, applyErrors.Count);

        return (new HrImportCommitResult(applied, applyErrors.Count, applyErrors), null);
    }

    private async Task<(List<ClassifiedRow> rows, string? error)> ClassifyAllAsync(
        string tenantId, Stream csvStream, CancellationToken ct)
    {
        var lines = await ReadLinesAsync(csvStream, ct);
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

        if (!header.Any(h => h.Trim().Equals("external_subject", StringComparison.OrdinalIgnoreCase)))
            return ([], "Missing required column 'external_subject'.");

        var colIndex = BuildColumnIndex(header);
        var rows = new List<ClassifiedRow>();
        var seenSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Count; i++)
        {
            var lineNo = i + 1;
            var fields = lines[i].Split(',');
            if (fields.Length < header.Length) { rows.Add(Rejected(lineNo, "", "Too few columns.")); continue; }

            var subject = GetField(fields, colIndex, "external_subject").Trim();
            if (string.IsNullOrEmpty(subject)) { rows.Add(Rejected(lineNo, subject, "external_subject is required.")); continue; }
            if (!seenSubjects.Add(subject)) { rows.Add(Rejected(lineNo, subject, "Duplicate external_subject.")); continue; }

            var roleString = GetField(fields, colIndex, "roles");
            var roles = roleString.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList();

            // Validate against EmployeeBootstrapService's known roles to catch mismatches at preview time.
            var invalidRole = roles.FirstOrDefault(r => !EmployeeBootstrapService.IsKnownRole(r));
            if (invalidRole is not null) { rows.Add(Rejected(lineNo, subject, $"Unknown role '{invalidRole}'.")); continue; }

            var parkingEligible = ParseBool(GetField(fields, colIndex, "parking_eligible"), out var peErr);
            var hasCompanyCar   = ParseBool(GetField(fields, colIndex, "has_company_car"),   out var hccErr);
            var accessibility   = ParseBool(GetField(fields, colIndex, "accessibility_eligible"), out var aeErr);
            var reserved        = ParseBool(GetField(fields, colIndex, "reserved_space_eligible"), out var rseErr);
            var isActive        = ParseBool(GetField(fields, colIndex, "active"), out var actErr);

            var boolErr = peErr ?? hccErr ?? aeErr ?? rseErr ?? actErr;
            if (boolErr is not null) { rows.Add(Rejected(lineNo, subject, boolErr)); continue; }

            var homeLocation = GetField(fields, colIndex, "home_location").Trim().NullIfEmpty();
            var email        = GetField(fields, colIndex, "email").Trim().NullIfEmpty();
            var subjectHash  = EmployeeBootstrapService.Hash(subject);
            var existing     = await profileRepository.GetAsync(tenantId, subjectHash, ct);

            var displayName = GetField(fields, colIndex, "display_name").NullIfEmpty();

            if (existing is null)
            {
                var createReq = new BootstrapEmployeeRequest(
                    subject, null, isActive, roles, email, homeLocation,
                    parkingEligible, hasCompanyCar, accessibility, reserved, "hr-import",
                    displayName);
                rows.Add(new ClassifiedRow(lineNo, subject, subjectHash, HrImportRowStatus.Created, null, createReq, null));
            }
            else
            {
                var updateReq = new UpdateEmployeeRequest(
                    isActive, roles, email, homeLocation,
                    parkingEligible, hasCompanyCar, accessibility, reserved,
                    displayName ?? existing.DisplayName);
                var status = IsUnchanged(existing, updateReq) ? HrImportRowStatus.Unchanged : HrImportRowStatus.Updated;
                rows.Add(new ClassifiedRow(lineNo, subject, subjectHash, status, null, null, updateReq));
            }
        }

        return (rows, null);
    }

    private static HrImportPreview ToPreview(List<ClassifiedRow> rows)
    {
        var publicRows = rows.Select(r => new HrImportRow(r.LineNumber, r.ExternalSubject, r.Status, r.Reason)).ToList();
        return new HrImportPreview(publicRows,
            publicRows.Count(r => r.Status == HrImportRowStatus.Created),
            publicRows.Count(r => r.Status == HrImportRowStatus.Updated),
            publicRows.Count(r => r.Status == HrImportRowStatus.Unchanged),
            publicRows.Count(r => r.Status == HrImportRowStatus.Rejected));
    }

    private static ClassifiedRow Rejected(int lineNo, string subject, string reason) =>
        new(lineNo, subject, "", HrImportRowStatus.Rejected, reason, null, null);

    private static bool IsUnchanged(UserProfile existing, UpdateEmployeeRequest req) =>
        existing.IsActive == req.IsActive &&
        existing.ParkingEligible == req.ParkingEligible &&
        existing.HasCompanyCar == req.HasCompanyCar &&
        existing.AccessibilityEligible == req.AccessibilityEligible &&
        existing.ReservedSpaceEligible == req.ReservedSpaceEligible &&
        existing.HomeLocationId == req.HomeLocationId &&
        existing.DisplayName == req.DisplayName;

    private static async Task<List<string>> ReadLinesAsync(Stream stream, CancellationToken ct)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('#') && !string.IsNullOrEmpty(trimmed))
                lines.Add(trimmed);
        }
        return lines;
    }

    private static Dictionary<string, int> BuildColumnIndex(string[] header)
    {
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++) idx[header[i].Trim()] = i;
        return idx;
    }

    private static string GetField(string[] fields, Dictionary<string, int> idx, string col) =>
        idx.TryGetValue(col, out var i) && i < fields.Length ? fields[i].Trim() : "";

    private static bool ParseBool(string value, out string? error)
    {
        if (string.IsNullOrEmpty(value)) { error = null; return false; }
        if (value.Equals("true",  StringComparison.OrdinalIgnoreCase)) { error = null; return true; }
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
