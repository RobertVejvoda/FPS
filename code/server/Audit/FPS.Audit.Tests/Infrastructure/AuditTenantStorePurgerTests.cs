using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Moq;

namespace FPS.Audit.Tests.Infrastructure;

public sealed class AuditTenantStorePurgerTests
{
    private readonly Mock<IAuditRetentionRepository> repository = new();

    [Fact]
    public void Purger_IsImmutableEvidence_AndNamedAudit()
    {
        var purger = new AuditTenantStorePurger(repository.Object);
        Assert.True(purger.IsImmutableEvidence);
        Assert.Equal("audit", purger.Service);
    }

    [Fact]
    public async Task PurgeAsync_WithoutSandboxReset_DeletesNothing_ReturnsZero()
    {
        var purger = new AuditTenantStorePurger(repository.Object);

        var count = await purger.PurgeAsync(TenantPurgeScope.For("demo"), sandboxReset: false, CancellationToken.None);

        Assert.Equal(0, count);
        repository.Verify(r => r.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WithSandboxReset_PurgesTenant()
    {
        repository
            .Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        var purger = new AuditTenantStorePurger(repository.Object);

        var count = await purger.PurgeAsync(TenantPurgeScope.For("demo"), sandboxReset: true, CancellationToken.None);

        Assert.Equal(4, count);
        repository.Verify(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()), Times.Once);
    }
}
