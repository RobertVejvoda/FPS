using Dapr.Client;
using FPS.Audit.Application.Privacy;
using FPS.Audit.Infrastructure;
using Moq;

namespace FPS.Audit.Tests.Infrastructure;

public sealed class DaprErasureRequestRepositoryTests
{
    private const string StoreName = "auditstore";
    private readonly Dictionary<string, object?> store = new();

    private DaprErasureRequestRepository BuildRepo()
    {
        var mock = new Mock<DaprClient>();

        mock.Setup(c => c.SaveStateAsync(StoreName, It.IsAny<string>(), It.IsAny<ErasureRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, ErasureRequest, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, key, value, _, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.GetStateAsync<ErasureRequest>(StoreName, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string key, ConsistencyMode? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                store.TryGetValue(key, out var v) ? v as ErasureRequest : null);

        return new DaprErasureRequestRepository(mock.Object);
    }

    private static ErasureRequest Build(string tenant = "demo") => new()
    {
        TenantId = tenant,
        TargetActorHash = "hash-target",
        RequestedByActorHash = "hash-requester",
        LegalBasis = "gdpr-article-17",
        Status = ErasureStatus.Pending,
    };

    // ── Restart persistence ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndGet_SurvivesRestart()
    {
        var repo1 = BuildRepo();
        var req = Build();
        await repo1.SaveAsync(req);

        var repo2 = BuildRepo();
        var found = await repo2.GetAsync(req.ErasureRequestId, "demo");

        Assert.NotNull(found);
        Assert.Equal(req.ErasureRequestId, found!.ErasureRequestId);
        Assert.Equal(ErasureStatus.Pending, found.Status);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var repo = BuildRepo();
        Assert.Null(await repo.GetAsync("does-not-exist", "demo"));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_TenantIsolation_ReturnsNull_ForOtherTenant()
    {
        var repo = BuildRepo();
        var req = Build("demo");
        await repo.SaveAsync(req);

        Assert.Null(await repo.GetAsync(req.ErasureRequestId, "other-co"));
    }

    // ── UpdateStatusAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_PersistsNewStatus()
    {
        var repo = BuildRepo();
        var req = Build();
        await repo.SaveAsync(req);

        await repo.UpdateStatusAsync(req.ErasureRequestId, "demo", ErasureStatus.Completed,
            completedAt: DateTime.UtcNow);

        var updated = await repo.GetAsync(req.ErasureRequestId, "demo");
        Assert.Equal(ErasureStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_NoOp_WhenNotFound()
    {
        var repo = BuildRepo();
        var ex = await Record.ExceptionAsync(() =>
            repo.UpdateStatusAsync("missing-id", "demo", ErasureStatus.Completed));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithServiceResults_PersistsResults()
    {
        var repo = BuildRepo();
        var req = Build();
        await repo.SaveAsync(req);

        var results = new[] { new ErasureServiceResult("fps-profile", ErasureTreatment.Deleted, 1) };
        await repo.UpdateStatusAsync(req.ErasureRequestId, "demo", ErasureStatus.InProgress,
            serviceResults: results);

        var updated = await repo.GetAsync(req.ErasureRequestId, "demo");
        Assert.Single(updated!.ServiceResults);
        Assert.Equal("fps-profile", updated.ServiceResults[0].Service);
    }
}
