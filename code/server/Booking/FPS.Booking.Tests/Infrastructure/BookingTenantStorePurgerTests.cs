using FPS.Booking.Application.Repositories;
using FPS.Booking.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Moq;
using Xunit;

namespace FPS.Booking.Tests.Infrastructure;

/// <summary>
/// The Booking store purger (PLAT003C) delegates to three owners — requests, draws and
/// correction audits — and reports the combined count so a sandbox reset can confirm every
/// tenant record was removed.
/// </summary>
public sealed class BookingTenantStorePurgerTests
{
    private readonly Mock<IBookingQueryRepository> repository = new();
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly Mock<ICorrectionAuditRepository> correctionAuditRepository = new();
    private readonly BookingTenantStorePurger purger;

    public BookingTenantStorePurgerTests()
    {
        purger = new BookingTenantStorePurger(
            repository.Object, drawRepository.Object, correctionAuditRepository.Object);
    }

    [Fact]
    public void Metadata_IsBookingAndMutable()
    {
        Assert.Equal("booking", purger.Service);
        Assert.False(purger.IsImmutableEvidence);
    }

    [Fact]
    public async Task PurgeAsync_ReturnsSumOfRequestDrawAndCorrectionCounts()
    {
        var scope = TenantPurgeScope.For("demo");
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(7);
        drawRepository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(4);
        correctionAuditRepository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var removed = await purger.PurgeAsync(scope, sandboxReset: true, CancellationToken.None);

        Assert.Equal(13, removed);
        repository.Verify(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()), Times.Once);
        drawRepository.Verify(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()), Times.Once);
        correctionAuditRepository.Verify(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()), Times.Once);
    }
}
