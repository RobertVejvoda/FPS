namespace FPS.Audit.Application.Privacy;

public interface IErasureRequestRepository
{
    Task SaveAsync(ErasureRequest request, CancellationToken cancellationToken = default);
    Task<ErasureRequest?> GetAsync(string erasureRequestId, string tenantId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string erasureRequestId, string tenantId, string status,
        IReadOnlyList<ErasureServiceResult>? serviceResults = null,
        string? blockReason = null,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default);
}
