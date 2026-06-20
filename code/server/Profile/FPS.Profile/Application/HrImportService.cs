using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace FPS.Profile.Application;

public enum HrImportRowStatus { Created, Updated, Unchanged, Rejected }
public enum HrVehicleImportStatus { Valid, Rejected }

public sealed record HrImportRow(
    int LineNumber,
    string ExternalSubject,
    HrImportRowStatus Status,
    string? Reason);

public sealed record HrVehicleImportRow(
    int LineNumber,
    string ExternalSubject,
    string LicensePlate,
    HrVehicleImportStatus Status,
    string? Reason);

public sealed record HrImportPreview(
    IReadOnlyList<HrImportRow> Rows,
    int Created,
    int Updated,
    int Unchanged,
    int Rejected,
    IReadOnlyList<HrVehicleImportRow> VehicleRows,
    int VehiclesValid,
    int VehiclesRejected);

public sealed record HrImportCommitResult(
    int Applied,
    int Rejected,
    IReadOnlyList<string> Errors,
    int VehiclesApplied,
    int VehiclesRejected,
    IReadOnlyList<string> VehicleErrors);

// Internal: full employee row data preserved across classification and apply phases.
internal sealed record ClassifiedRow(
    int LineNumber,
    string ExternalSubject,
    string SubjectHash,
    HrImportRowStatus Status,
    string? Reason,
    BootstrapEmployeeRequest? CreateRequest,
    UpdateEmployeeRequest? UpdateRequest);

// Internal: full vehicle row data preserved across classification and apply phases.
internal sealed record ClassifiedVehicleRow(
    int LineNumber,
    string ExternalSubject,
    string SubjectHash,
    string LicensePlate,
    string? Alias,
    string VehicleType,
    bool IsElectric,
    bool IsActive,
    HrVehicleImportStatus Status,
    string? Reason);

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

    private static readonly string[] VehicleColumns =
    [
        "external_subject", "vehicle_alias", "vehicle_license_plate",
        "vehicle_type", "vehicle_is_electric", "active"
    ];

    private static readonly string[] ValidVehicleTypes = ["car", "motorcycle", "van"];

    private static readonly HashSet<string> ForbiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "secret", "token", "credential", "ssn",
        "national_id", "salary", "employee_id", "manager_notes", "department"
    };

    public async Task<(HrImportPreview? preview, string? error)> PreviewAsync(
        string tenantId, Stream csvStream, Stream? vehicleStream, CancellationToken ct)
    {
        var (rows, parseError) = await ClassifyAllAsync(tenantId, csvStream, ct);
        if (parseError is not null) return (null, parseError);

        List<ClassifiedVehicleRow> vehicleRows = [];
        if (vehicleStream is not null)
        {
            // Build the set of known subject hashes from the employee batch.
            var batchHashes = rows
                .Select(r => r.SubjectHash)
                .Where(h => !string.IsNullOrEmpty(h))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string? vehicleError;
            (vehicleRows, vehicleError) = await ClassifyVehiclesAsync(tenantId, vehicleStream, batchHashes, ct);
            if (vehicleError is not null) return (null, vehicleError);
        }

        var preview = ToPreview(rows, vehicleRows);
        logger.LogInformation(
            "HR import preview: tenantId={TenantId} actor={Actor} created={Created} updated={Updated} unchanged={Unchanged} rejected={Rejected} vehiclesValid={VehiclesValid} vehiclesRejected={VehiclesRejected}",
            tenantId, currentUser.UserId, preview.Created, preview.Updated, preview.Unchanged, preview.Rejected,
            preview.VehiclesValid, preview.VehiclesRejected);

        return (preview, null);
    }

    public async Task<(HrImportCommitResult? result, string? error)> CommitAsync(
        string tenantId, Stream csvStream, Stream? vehicleStream, CancellationToken ct)
    {
        // Phase 1: classify all employee rows — no DB writes.
        var (rows, parseError) = await ClassifyAllAsync(tenantId, csvStream, ct);
        if (parseError is not null) return (null, parseError);

        // Classify vehicle rows if provided.
        List<ClassifiedVehicleRow> vehicleRows = [];
        if (vehicleStream is not null)
        {
            var batchHashes = rows
                .Select(r => r.SubjectHash)
                .Where(h => !string.IsNullOrEmpty(h))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string? vehicleError;
            (vehicleRows, vehicleError) = await ClassifyVehiclesAsync(tenantId, vehicleStream, batchHashes, ct);
            if (vehicleError is not null) return (null, vehicleError);
        }

        // Reject the entire commit if any employee row failed validation.
        var rejectedEmployees = rows.Where(r => r.Status == HrImportRowStatus.Rejected).ToList();
        var rejectedVehicles  = vehicleRows.Where(r => r.Status == HrVehicleImportStatus.Rejected).ToList();

        if (rejectedEmployees.Count > 0 || rejectedVehicles.Count > 0)
        {
            var errors = rejectedEmployees
                .Select(r => $"Line {r.LineNumber} ({r.ExternalSubject}): {r.Reason}")
                .Concat(rejectedVehicles.Select(r => $"Vehicle line {r.LineNumber} ({r.ExternalSubject}): {r.Reason}"))
                .ToList();
            return (new HrImportCommitResult(
                0, rejectedEmployees.Count, errors,
                0, rejectedVehicles.Count, []), null);
        }

        // Phase 2: apply all valid employee rows.
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

        // If any employee apply errors occurred, stop before touching vehicles —
        // partial employee state must not be mixed with vehicle writes.
        if (applyErrors.Count > 0)
        {
            logger.LogWarning(
                "HR import aborted vehicle phase: tenantId={TenantId} actor={Actor} applyErrors={ApplyErrors}",
                tenantId, currentUser.UserId, applyErrors.Count);
            return (new HrImportCommitResult(
                applied, applyErrors.Count, applyErrors,
                0, 0, []), null);
        }

        // Phase 3: apply vehicle rows after employees are persisted.
        var (vehiclesApplied, vehiclesRejected, vehicleApplyErrors) = await ApplyVehicleRowsAsync(tenantId, vehicleRows, ct);

        logger.LogInformation(
            "HR import committed: tenantId={TenantId} actor={Actor} applied={Applied} vehiclesApplied={VehiclesApplied} vehicleErrors={VehicleErrors}",
            tenantId, currentUser.UserId, applied, vehiclesApplied, vehicleApplyErrors.Count);

        return (new HrImportCommitResult(
            applied, applyErrors.Count, applyErrors,
            vehiclesApplied, vehiclesRejected, vehicleApplyErrors), null);
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
            var displayName  = GetField(fields, colIndex, "display_name").Trim().NullIfEmpty();
            var subjectHash  = EmployeeBootstrapService.Hash(subject);
            var existing     = await profileRepository.GetAsync(tenantId, subjectHash, ct);

            if (existing is null)
            {
                var createReq = new BootstrapEmployeeRequest(
                    subject, null, isActive, roles, email, homeLocation,
                    parkingEligible, hasCompanyCar, accessibility, reserved, "hr-import", displayName);
                rows.Add(new ClassifiedRow(lineNo, subject, subjectHash, HrImportRowStatus.Created, null, createReq, null));
            }
            else
            {
                var updateReq = new UpdateEmployeeRequest(
                    isActive, roles, email, homeLocation,
                    parkingEligible, hasCompanyCar, accessibility, reserved, displayName);
                var status = IsUnchanged(existing, updateReq) ? HrImportRowStatus.Unchanged : HrImportRowStatus.Updated;
                rows.Add(new ClassifiedRow(lineNo, subject, subjectHash, status, null, null, updateReq));
            }
        }

        return (rows, null);
    }

    private async Task<(List<ClassifiedVehicleRow> rows, string? error)> ClassifyVehiclesAsync(
        string tenantId,
        Stream vehicleCsvStream,
        IReadOnlySet<string> employeeBatchSubjectHashes,
        CancellationToken ct)
    {
        var lines = await ReadLinesAsync(vehicleCsvStream, ct);
        if (lines.Count == 0) return ([], "Vehicles CSV is empty or contains only comments.");

        var header = lines[0].Split(',');
        foreach (var col in header)
        {
            var c = col.Trim();
            if (ForbiddenColumns.Contains(c))
                return ([], $"Forbidden vehicle column '{c}' — do not include secrets or personal data.");
            if (!VehicleColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                return ([], $"Unknown vehicle column '{c}' — only the documented column set is allowed.");
        }

        if (!header.Any(h => h.Trim().Equals("external_subject", StringComparison.OrdinalIgnoreCase)))
            return ([], "Missing required vehicle column 'external_subject'.");
        if (!header.Any(h => h.Trim().Equals("vehicle_license_plate", StringComparison.OrdinalIgnoreCase)))
            return ([], "Missing required vehicle column 'vehicle_license_plate'.");

        var colIndex = BuildColumnIndex(header);
        var rows = new List<ClassifiedVehicleRow>();
        var seenPlates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Count; i++)
        {
            var lineNo = i + 1;
            var fields = lines[i].Split(',');
            if (fields.Length < header.Length) { rows.Add(VehicleRejected(lineNo, "", "", "Too few columns.")); continue; }

            var subject = GetField(fields, colIndex, "external_subject").Trim();
            if (string.IsNullOrEmpty(subject)) { rows.Add(VehicleRejected(lineNo, "", "", "external_subject is required.")); continue; }

            var plate = GetField(fields, colIndex, "vehicle_license_plate").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(plate)) { rows.Add(VehicleRejected(lineNo, subject, "", "vehicle_license_plate is required.")); continue; }

            if (!seenPlates.Add(plate)) { rows.Add(VehicleRejected(lineNo, subject, plate, "Duplicate vehicle_license_plate in this file.")); continue; }

            var vehicleType = GetField(fields, colIndex, "vehicle_type").Trim().ToLowerInvariant();
            if (!ValidVehicleTypes.Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(VehicleRejected(lineNo, subject, plate, $"Unknown vehicle_type '{vehicleType}' — valid: car, motorcycle, van."));
                continue;
            }

            var isElectric = ParseBool(GetField(fields, colIndex, "vehicle_is_electric"), out var elErr);
            var isActive   = ParseBool(GetField(fields, colIndex, "active"),               out var actErr);
            var boolErr = elErr ?? actErr;
            if (boolErr is not null) { rows.Add(VehicleRejected(lineNo, subject, plate, boolErr)); continue; }

            var subjectHash = EmployeeBootstrapService.Hash(subject);

            // Subject must be in the employee batch or exist as a profile in this tenant.
            if (!employeeBatchSubjectHashes.Contains(subjectHash))
            {
                var existingProfile = await profileRepository.GetAsync(tenantId, subjectHash, ct);
                if (existingProfile is null)
                {
                    rows.Add(VehicleRejected(lineNo, subject, plate,
                        "external_subject does not match any employee in this import or an existing profile."));
                    continue;
                }
            }

            var alias = GetField(fields, colIndex, "vehicle_alias").Trim().NullIfEmpty();
            rows.Add(new ClassifiedVehicleRow(lineNo, subject, subjectHash, plate, alias, vehicleType, isElectric, isActive, HrVehicleImportStatus.Valid, null));
        }

        return (rows, null);
    }

    private async Task<(int applied, int rejected, List<string> errors)> ApplyVehicleRowsAsync(
        string tenantId,
        List<ClassifiedVehicleRow> vehicleRows,
        CancellationToken ct)
    {
        var applied = 0;
        var rejected = 0;
        var errors = new List<string>();

        // Group by subject hash so each profile is loaded and saved at most once.
        var bySubject = vehicleRows
            .Where(r => r.Status == HrVehicleImportStatus.Valid)
            .GroupBy(r => r.SubjectHash, StringComparer.OrdinalIgnoreCase);

        foreach (var group in bySubject)
        {
            var profile = await profileRepository.GetAsync(tenantId, group.Key, ct);
            if (profile is null)
            {
                foreach (var row in group)
                {
                    errors.Add($"Vehicle line {row.LineNumber} ({row.ExternalSubject}): Profile not found at commit time.");
                    rejected++;
                }
                continue;
            }

            var updatedVehicles = profile.Vehicles.ToList();
            foreach (var row in group)
            {
                var existing = updatedVehicles.FirstOrDefault(v =>
                    string.Equals(v.LicensePlate, row.LicensePlate, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    // Update all imported facts (alias, type, electric, active) while
                    // preserving VehicleId and default-slot semantics.
                    var idx = updatedVehicles.IndexOf(existing);
                    updatedVehicles[idx] = existing with
                    {
                        Alias = row.Alias ?? existing.Alias,
                        VehicleType = row.VehicleType,
                        IsElectric = row.IsElectric,
                        IsActive = row.IsActive,
                    };
                    applied++;
                }
                else
                {
                    // New vehicle — first active vehicle in profile becomes the default.
                    var isFirstActive = row.IsActive && !updatedVehicles.Any(v => v.IsActive);
                    updatedVehicles.Add(new Vehicle(
                        Guid.NewGuid().ToString(),
                        row.LicensePlate,
                        row.VehicleType,
                        row.IsElectric,
                        row.IsActive,
                        IsDefault: isFirstActive,
                        Alias: row.Alias));
                    applied++;
                }
            }

            await profileRepository.SaveAsync(ProfileWithVehicles(profile, updatedVehicles), ct);
        }

        return (applied, rejected, errors);
    }

    private static HrImportPreview ToPreview(List<ClassifiedRow> rows, List<ClassifiedVehicleRow> vehicleRows)
    {
        var publicRows = rows.Select(r => new HrImportRow(r.LineNumber, r.ExternalSubject, r.Status, r.Reason)).ToList();
        var publicVehicleRows = vehicleRows
            .Select(r => new HrVehicleImportRow(r.LineNumber, r.ExternalSubject, r.LicensePlate, r.Status, r.Reason))
            .ToList();
        return new HrImportPreview(
            publicRows,
            publicRows.Count(r => r.Status == HrImportRowStatus.Created),
            publicRows.Count(r => r.Status == HrImportRowStatus.Updated),
            publicRows.Count(r => r.Status == HrImportRowStatus.Unchanged),
            publicRows.Count(r => r.Status == HrImportRowStatus.Rejected),
            publicVehicleRows,
            publicVehicleRows.Count(r => r.Status == HrVehicleImportStatus.Valid),
            publicVehicleRows.Count(r => r.Status == HrVehicleImportStatus.Rejected));
    }

    private static ClassifiedRow Rejected(int lineNo, string subject, string reason) =>
        new(lineNo, subject, "", HrImportRowStatus.Rejected, reason, null, null);

    private static ClassifiedVehicleRow VehicleRejected(int lineNo, string subject, string plate, string reason) =>
        new(lineNo, subject, "", plate, null, "", false, false, HrVehicleImportStatus.Rejected, reason);

    private static bool IsUnchanged(UserProfile existing, UpdateEmployeeRequest req) =>
        existing.IsActive == req.IsActive &&
        existing.ParkingEligible == req.ParkingEligible &&
        existing.HasCompanyCar == req.HasCompanyCar &&
        existing.AccessibilityEligible == req.AccessibilityEligible &&
        existing.ReservedSpaceEligible == req.ReservedSpaceEligible &&
        existing.HomeLocationId == req.HomeLocationId &&
        (req.DisplayName is null || existing.DisplayName == req.DisplayName);

    private static UserProfile ProfileWithVehicles(UserProfile p, IReadOnlyList<Vehicle> vehicles) => new()
    {
        TenantId = p.TenantId,
        UserId = p.UserId,
        Status = p.Status,
        ParkingEligible = p.ParkingEligible,
        HasCompanyCar = p.HasCompanyCar,
        AccessibilityEligible = p.AccessibilityEligible,
        ReservedSpaceEligible = p.ReservedSpaceEligible,
        EmployeeId = p.EmployeeId,
        DisplayName = p.DisplayName,
        FpsRoles = p.FpsRoles,
        NotificationAddress = p.NotificationAddress,
        HomeLocationId = p.HomeLocationId,
        Vehicles = vehicles,
        SnapshotVersion = Guid.NewGuid().ToString(),
        FactSource = p.FactSource,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

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
