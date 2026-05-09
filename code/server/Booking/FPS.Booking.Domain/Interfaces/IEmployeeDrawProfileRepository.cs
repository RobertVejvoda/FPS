using FPS.Booking.Domain.Entities;

namespace FPS.Booking.Domain.Interfaces;

public interface IEmployeeDrawProfileRepository
{
    Task<EmployeeDrawProfile?> GetByEmployeeIdAsync(UserId employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeDrawProfile>> GetByEmployeeIdsAsync(IEnumerable<UserId> employeeIds, CancellationToken cancellationToken = default);
    Task SaveAsync(EmployeeDrawProfile profile, CancellationToken cancellationToken = default);
    Task SaveBatchAsync(IEnumerable<EmployeeDrawProfile> profiles, CancellationToken cancellationToken = default);
}
