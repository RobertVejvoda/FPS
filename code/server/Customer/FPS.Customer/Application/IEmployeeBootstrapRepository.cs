using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface IEmployeeBootstrapRepository
{
    Task<EmployeeBootstrapRecord?> GetAsync(string tenantId, string subjectHash, CancellationToken ct);
    Task<bool> SubjectExistsAsync(string tenantId, string subjectHash, CancellationToken ct);
    Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken ct);
    Task SaveAsync(EmployeeBootstrapRecord record, CancellationToken ct);
    Task<IReadOnlyList<EmployeeBootstrapRecord>> ListAsync(string tenantId, CancellationToken ct);
}
