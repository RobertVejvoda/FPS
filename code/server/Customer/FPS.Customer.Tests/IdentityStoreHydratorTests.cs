using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Customer.Tests;

public sealed class IdentityStoreHydratorTests
{
    private readonly InMemoryTenantIdentityConfigStore configStore = new();
    private readonly InMemoryTenantRoleMappingStore roleMappingStore;

    public IdentityStoreHydratorTests()
    {
        roleMappingStore = new InMemoryTenantRoleMappingStore(configStore);
    }

    private IdentityStoreHydrator Build(ITenantIdentityRepository repository, bool isDevelopment)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName)
           .Returns(isDevelopment ? Environments.Development : Environments.Production);
        return new IdentityStoreHydrator(
            repository,
            configStore,
            roleMappingStore,
            NullLogger<IdentityStoreHydrator>.Instance,
            env.Object);
    }

    // ── PERSIST006B fail-closed regression ───────────────────────────────────────
    // In non-Development profiles, HydrateAsync must propagate repository exceptions.
    // The process must crash before app.Run() — the orchestrator restarts the pod
    // once Dapr is available. This is the primary guard against the fail-open path
    // in TenantClaimsTransformation (IsEnforcementActive==false passes raw roles
    // when the config store is empty).

    [Fact]
    public async Task HydrateAsync_RepositoryThrowsInProduction_PropagatesException()
    {
        var repo = new Mock<ITenantIdentityRepository>();
        repo.Setup(r => r.GetConfiguredTenantIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dapr state store unavailable"));

        var hydrator = Build(repo.Object, isDevelopment: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => hydrator.HydrateAsync());
    }

    [Fact]
    public async Task HydrateAsync_RepositoryThrowsInDevelopment_DoesNotThrow_StoresRemainEmpty()
    {
        var repo = new Mock<ITenantIdentityRepository>();
        repo.Setup(r => r.GetConfiguredTenantIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dapr state store unavailable"));

        var hydrator = Build(repo.Object, isDevelopment: true);

        await hydrator.HydrateAsync(); // must not throw

        Assert.False(configStore.IsEnforcementActive); // stores stay empty
    }

    [Fact]
    public async Task HydrateAsync_SuccessfulHydration_PopulatesConfigAndRoleMapping()
    {
        var repo = new InMemoryTenantIdentityRepository();
        await repo.SaveConfigAsync(new TenantIdentityConfig
        {
            TenantId = "tenant-x",
            TrustedIssuer = "https://idp.example.com",
            Audience = "fps-api",
            TenantClaimName = "tenant_id",
            SubjectClaimName = "sub",
            RoleClaimNames = ["groups"],
            RoleMapping = new Dictionary<string, string> { ["idp-admins"] = "admin" },
            ConfiguredByHash = "hash",
            ConfiguredAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        var hydrator = Build(repo, isDevelopment: false);

        await hydrator.HydrateAsync();

        Assert.True(configStore.IsConfigured("tenant-x"));
        Assert.True(configStore.IsEnforcementActive);
        Assert.Equal(["admin"], roleMappingStore.MapToRoles("tenant-x", ["idp-admins"]));
    }
}
