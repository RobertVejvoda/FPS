using Dapr.Client;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using Moq;

namespace FPS.Customer.Tests;

public sealed class DaprTenantRequestRepositoryTests
{
    private const string Store = "customerstore";
    private const string IndexKey = "tenant-requests:index";

    private static TenantRequest Request(string id) => new()
    {
        RequestId = id,
        Company = "Acme",
        PrimaryDomain = "acme.com",
        ContactEmail = "jo@acme.com",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task SaveAsync_RetriesIndexWrite_OnStaleETag()
    {
        var dapr = new Mock<DaprClient>();

        // A fresh empty index on every read, so the retry re-reads rather than reusing a mutated list.
        dapr.Setup(c => c.GetStateAndETagAsync<List<string>>(Store, IndexKey, null, null, default))
            .ReturnsAsync(() => (new List<string>(), "etag-current"));

        // The first index write loses the ETag race (stale); the retry wins.
        dapr.SetupSequence(c => c.TrySaveStateAsync(
                Store, IndexKey, It.IsAny<List<string>>(), "etag-current", null, null, default))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var repo = new DaprTenantRequestRepository(dapr.Object);

        await repo.SaveAsync(Request("req-1"), CancellationToken.None);

        // Record written once; the index compare-and-swap was retried (with req-1) until it succeeded.
        dapr.Verify(c => c.SaveStateAsync(
            Store, "tenant-request:req-1", It.IsAny<TenantRequest>(), null, null, default), Times.Once);
        dapr.Verify(c => c.TrySaveStateAsync(
            Store, IndexKey, It.Is<List<string>>(l => l.Contains("req-1")), "etag-current", null, null, default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SaveAsync_IndexWriteKeepsLosing_Throws()
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(c => c.GetStateAndETagAsync<List<string>>(Store, IndexKey, null, null, default))
            .ReturnsAsync(() => (new List<string>(), "etag-current"));
        dapr.Setup(c => c.TrySaveStateAsync(
                Store, IndexKey, It.IsAny<List<string>>(), "etag-current", null, null, default))
            .ReturnsAsync(false);

        var repo = new DaprTenantRequestRepository(dapr.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.SaveAsync(Request("req-2"), CancellationToken.None));
    }
}
