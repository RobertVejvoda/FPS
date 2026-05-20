using FPS.Audit.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Audit.Application;

public sealed record RetentionExecutionResult(
    string TenantId,
    int PolicyRetentionDays,
    DateTime Cutoff,
    int CandidateCount,
    int DeletedCount,
    DateTime ExecutedAt,
    string Result,
    string? ErrorCategory = null);

public sealed class AuditRetentionService(
    IAuditRetentionRepository repository,
    ILogger<AuditRetentionService> logger)
{
    public async Task<RetentionExecutionResult> ExecuteAsync(
        AuditRetentionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = policy.CutoffUtc(now);

        try
        {
            var candidateCount = await repository.CountOlderThanAsync(policy.TenantId, cutoff, cancellationToken);
            var deletedCount = await repository.DeleteOlderThanAsync(policy.TenantId, cutoff, cancellationToken);

            logger.LogInformation(
                "Audit retention completed. Tenant={TenantId} RetentionDays={RetentionDays} Cutoff={Cutoff:O} Candidates={CandidateCount} Deleted={DeletedCount}",
                policy.TenantId, policy.RetentionDays, cutoff, candidateCount, deletedCount);

            return new RetentionExecutionResult(
                TenantId: policy.TenantId,
                PolicyRetentionDays: policy.RetentionDays,
                Cutoff: cutoff,
                CandidateCount: candidateCount,
                DeletedCount: deletedCount,
                ExecutedAt: now,
                Result: "completed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit retention failed. Tenant={TenantId}", policy.TenantId);
            return new RetentionExecutionResult(
                TenantId: policy.TenantId,
                PolicyRetentionDays: policy.RetentionDays,
                Cutoff: cutoff,
                CandidateCount: 0,
                DeletedCount: 0,
                ExecutedAt: now,
                Result: "failed",
                ErrorCategory: ex.GetType().Name);
        }
    }
}
