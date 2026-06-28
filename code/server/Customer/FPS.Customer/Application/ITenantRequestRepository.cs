using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface ITenantRequestRepository
{
    Task SaveAsync(TenantRequest request, CancellationToken ct);
    Task<TenantRequest?> GetAsync(string requestId, CancellationToken ct);
    Task<IReadOnlyList<TenantRequest>> ListAsync(CancellationToken ct);

    /// <summary>True if an undecided (Requested) request already exists for this contact email.</summary>
    Task<bool> HasOpenRequestForEmailAsync(string contactEmail, CancellationToken ct);
}
