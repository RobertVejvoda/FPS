using FPS.Audit.Application.Privacy;
using FPS.Audit.Infrastructure;

namespace FPS.Audit.Tests;

public sealed class ErasureRequestRepositoryTests
{
    private readonly InMemoryErasureRequestRepository repo = new();

    private static ErasureRequest Build(string tenant = "t1", string status = ErasureStatus.Pending) => new()
    {
        TenantId = tenant,
        TargetActorHash = "hash-target",
        RequestedByActorHash = "hash-requester",
        LegalBasis = "gdpr-article-17",
        Status = status,
    };

    [Fact]
    public async Task SaveAndGet_RoundTrip()
    {
        var req = Build();
        await repo.SaveAsync(req);

        var found = await repo.GetAsync(req.ErasureRequestId, "t1");

        Assert.NotNull(found);
        Assert.Equal(req.ErasureRequestId, found.ErasureRequestId);
        Assert.Equal(ErasureStatus.Pending, found.Status);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNotFound()
    {
        var result = await repo.GetAsync("does-not-exist", "t1");
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_TenantIsolation_ReturnsNull_ForWrongTenant()
    {
        var req = Build("t1");
        await repo.SaveAsync(req);

        var result = await repo.GetAsync(req.ErasureRequestId, "t2");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatus_ChangesStatus()
    {
        var req = Build();
        await repo.SaveAsync(req);

        await repo.UpdateStatusAsync(req.ErasureRequestId, "t1", ErasureStatus.Completed,
            completedAt: DateTime.UtcNow);

        var updated = await repo.GetAsync(req.ErasureRequestId, "t1");
        Assert.Equal(ErasureStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateStatus_SetsServiceResults()
    {
        var req = Build();
        await repo.SaveAsync(req);
        var results = new List<ErasureServiceResult>
        {
            new("profile", ErasureTreatment.Deleted, 3),
            new("booking", ErasureTreatment.Anonymised, 2),
        };

        await repo.UpdateStatusAsync(req.ErasureRequestId, "t1", ErasureStatus.Completed,
            serviceResults: results, completedAt: DateTime.UtcNow);

        var updated = await repo.GetAsync(req.ErasureRequestId, "t1");
        Assert.Equal(2, updated!.ServiceResults.Count);
    }

    [Fact]
    public async Task UpdateStatus_SetsBlockReason()
    {
        var req = Build();
        await repo.SaveAsync(req);

        await repo.UpdateStatusAsync(req.ErasureRequestId, "t1", ErasureStatus.Blocked,
            blockReason: "Active booking exists.");

        var updated = await repo.GetAsync(req.ErasureRequestId, "t1");
        Assert.Equal(ErasureStatus.Blocked, updated!.Status);
        Assert.Equal("Active booking exists.", updated.BlockReason);
    }

    [Fact]
    public async Task UpdateStatus_Noop_ForUnknownRequest()
    {
        // Must not throw
        var ex = await Record.ExceptionAsync(() =>
            repo.UpdateStatusAsync("nonexistent", "t1", ErasureStatus.Completed));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UpdateStatus_TenantIsolation_DoesNotUpdateOtherTenant()
    {
        var req = Build("t1");
        await repo.SaveAsync(req);

        await repo.UpdateStatusAsync(req.ErasureRequestId, "t2", ErasureStatus.Completed);

        var original = await repo.GetAsync(req.ErasureRequestId, "t1");
        Assert.Equal(ErasureStatus.Pending, original!.Status);
    }
}
