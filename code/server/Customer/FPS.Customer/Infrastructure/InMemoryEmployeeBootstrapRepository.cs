using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class InMemoryEmployeeBootstrapRepository : IEmployeeBootstrapRepository
{
    // Key: "{tenantId}:{subjectHash}"
    private readonly ConcurrentDictionary<string, EmployeeBootstrapRecord> bySubject =
        new(StringComparer.OrdinalIgnoreCase);
    // Key: "{tenantId}:{employeeId}"
    private readonly ConcurrentDictionary<string, bool> employeeIds =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<EmployeeBootstrapRecord?> GetAsync(string tenantId, string subjectHash, CancellationToken ct) =>
        Task.FromResult(bySubject.TryGetValue(Key(tenantId, subjectHash), out var r) ? r : null);

    public Task<bool> SubjectExistsAsync(string tenantId, string subjectHash, CancellationToken ct) =>
        Task.FromResult(bySubject.ContainsKey(Key(tenantId, subjectHash)));

    public Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken ct) =>
        Task.FromResult(employeeIds.ContainsKey(Key(tenantId, employeeId)));

    public Task SaveAsync(EmployeeBootstrapRecord record, CancellationToken ct)
    {
        bySubject[Key(record.TenantId, record.ExternalSubjectHash)] = record;
        if (record.EmployeeId is not null)
            employeeIds[Key(record.TenantId, record.EmployeeId)] = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmployeeBootstrapRecord>> ListAsync(string tenantId, CancellationToken ct)
    {
        var results = bySubject.Values
            .Where(r => string.Equals(r.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.RecordedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<EmployeeBootstrapRecord>>(results);
    }

    private static string Key(string a, string b) => $"{a}:{b}";
}
