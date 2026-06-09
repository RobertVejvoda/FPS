using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Infrastructure.Repositories;
using Moq;
using Xunit;

namespace FPS.Booking.Infrastructure.Tests.Repositories;

public sealed class DaprDrawRepositoryTests
{
    private readonly Mock<DaprClient> mockDaprClient = new();
    private readonly DaprDrawRepository repository;

    private const string TestDrawKey = "draw:tenant-1:loc-1:2026-06-02:0900";
    private const string TestETag = "etag-12345";

    public DaprDrawRepositoryTests()
    {
        repository = new DaprDrawRepository(mockDaprClient.Object);
    }

    // ── GetByKeyAsync: retrieves ETag with state ──────────────────────────────

    [Fact]
    public async Task GetByKeyAsync_ReturnsValueWithETag()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress" };
        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<DrawAttemptDto>("bookingstore", TestDrawKey, null, null, default))
            .ReturnsAsync((attempt, TestETag));

        var result = await repository.GetByKeyAsync(TestDrawKey);

        Assert.NotNull(result);
        Assert.Equal(TestDrawKey, result.DrawKey);
        Assert.Equal(TestETag, result.ETag);
    }

    [Fact]
    public async Task GetByKeyAsync_NoState_ReturnsNull()
    {
        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<DrawAttemptDto>("bookingstore", TestDrawKey, null, null, default))
            .ReturnsAsync((null!, string.Empty));

        var result = await repository.GetByKeyAsync(TestDrawKey);

        Assert.Null(result);
    }

    // ── SaveAsync: without ETag uses simple save ─────────────────────────────

    [Fact]
    public async Task SaveAsync_WithoutETag_UsesSimpleSave()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = null };

        await repository.SaveAsync(attempt);

        mockDaprClient.Verify(c => c.SaveStateAsync(
            "bookingstore", TestDrawKey, attempt, null, null, default), Times.Once);
    }

    // ── SaveAsync: with ETag uses optimistic concurrency ─────────────────────

    [Fact]
    public async Task SaveAsync_WithETag_UsesOptimisticConcurrency()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = TestETag };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, attempt, TestETag, null, null, default))
            .ReturnsAsync(true);

        await repository.SaveAsync(attempt);

        mockDaprClient.Verify(c => c.TrySaveStateAsync(
            "bookingstore", TestDrawKey, attempt, TestETag, null, null, default), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithETag_Conflict_ThrowsInvalidOperationException()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = TestETag };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, attempt, TestETag, null, null, default))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(attempt));

        Assert.Contains("ETag mismatch", exception.Message);
        Assert.Contains("concurrent modification", exception.Message);
    }

    // ── TrySaveAsync: returns success/failure without throwing ───────────────

    [Fact]
    public async Task TrySaveAsync_WithoutETag_AlwaysReturnsTrue()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = null };

        var result = await repository.TrySaveAsync(attempt);

        Assert.True(result);
        mockDaprClient.Verify(c => c.SaveStateAsync(
            "bookingstore", TestDrawKey, attempt, null, null, default), Times.Once);
    }

    [Fact]
    public async Task TrySaveAsync_WithETag_Success_ReturnsTrue()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = TestETag };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, attempt, TestETag, null, null, default))
            .ReturnsAsync(true);

        var result = await repository.TrySaveAsync(attempt);

        Assert.True(result);
    }

    [Fact]
    public async Task TrySaveAsync_WithETag_Conflict_ReturnsFalse()
    {
        var attempt = new DrawAttemptDto { DrawKey = TestDrawKey, Status = "InProgress", ETag = TestETag };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, attempt, TestETag, null, null, default))
            .ReturnsAsync(false);

        var result = await repository.TrySaveAsync(attempt);

        Assert.False(result);
    }

    // ── Concurrency control prevents lost updates ────────────────────────────

    [Fact]
    public async Task ConcurrentUpdates_ETagMismatch_PreventLostUpdates()
    {
        // Simulate two concurrent readers getting the same initial state
        var initialETag = "etag-initial";

        // First update succeeds
        var updatedAttempt1 = new DrawAttemptDto
        {
            DrawKey = TestDrawKey,
            Status = "InProgress",
            AllocatedCount = 5,
            ETag = initialETag
        };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, updatedAttempt1, initialETag, null, null, default))
            .ReturnsAsync(true);

        // Second update with stale ETag fails
        var updatedAttempt2 = new DrawAttemptDto
        {
            DrawKey = TestDrawKey,
            Status = "InProgress",
            AllocatedCount = 3,
            ETag = initialETag
        };
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync("bookingstore", TestDrawKey, updatedAttempt2, initialETag, null, null, default))
            .ReturnsAsync(false);

        var result1 = await repository.TrySaveAsync(updatedAttempt1);
        var result2 = await repository.TrySaveAsync(updatedAttempt2);

        Assert.True(result1);
        Assert.False(result2); // Lost update prevented
    }
}
