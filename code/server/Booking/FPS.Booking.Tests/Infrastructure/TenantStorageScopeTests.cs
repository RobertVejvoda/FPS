using FPS.SharedKernel.Infrastructure;

namespace FPS.Booking.Infrastructure.Tests;

public sealed class TenantStorageScopeTests
{
    // ── Collection / partition naming ─────────────────────────────────────────

    [Fact]
    public void Collection_IsDeterministicAndSanitised()
    {
        Assert.Equal("fairspot-acme-corp-booking", TenantStorageScope.Collection("booking", "acme-corp"));
        Assert.Equal("fairspot-acme-corp-booking", TenantStorageScope.Collection("Booking", "ACME-CORP")); // normalised
    }

    [Fact]
    public void Collection_DifferentTenants_ProduceDifferentNames()
        => Assert.NotEqual(
            TenantStorageScope.Collection("booking", "tenant-a"),
            TenantStorageScope.Collection("booking", "tenant-b"));

    [Theory]
    [InlineData("ab")]            // too short
    [InlineData("fairspot-internal")]  // reserved prefix
    [InlineData("Bad Tenant")]    // invalid characters
    public void Collection_InvalidTenant_Throws(string tenantId)
        => Assert.Throws<ArgumentException>(() => TenantStorageScope.Collection("booking", tenantId));

    [Fact]
    public void Collection_InvalidService_Throws()
        => Assert.Throws<ArgumentException>(() => TenantStorageScope.Collection("bad service!", "acme"));

    [Fact]
    public void Collection_LongTenant_StaysWithinIdentifierLimit_DeterministicAndCollisionFree()
    {
        var tenant63 = new string('a', 63); // the contract's max-length tenant id

        var name1 = TenantStorageScope.Collection("configuration", tenant63);
        var name2 = TenantStorageScope.Collection("configuration", tenant63);

        Assert.True(name1.Length <= TenantStorageScope.MaxNameLength,
            $"'{name1}' ({name1.Length}) exceeds {TenantStorageScope.MaxNameLength}");
        Assert.Equal(name1, name2); // deterministic

        // A different long tenant must not collide on the same service.
        var other = "b" + new string('a', 62);
        Assert.NotEqual(name1, TenantStorageScope.Collection("configuration", other));
    }

    [Fact]
    public void KeyPrefix_AndSegment_ScopeToTenant()
    {
        Assert.Equal("request:acme:", TenantStorageScope.KeyPrefix("request", "ACME"));
        Assert.Equal(":acme:", TenantStorageScope.KeySegment("acme"));
    }

    // ── Purge scope ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void PurgeScope_For_BlankOrNullTenant_Throws(string? tenantId)
        => Assert.ThrowsAny<ArgumentException>(() => TenantPurgeScope.For(tenantId!));

    [Fact]
    public void PurgeScope_AddressesOnlyTheNamedTenant()
    {
        var a = TenantPurgeScope.For("tenant-a");

        Assert.Equal("tenant-a", a.TenantId);
        Assert.Equal(":tenant-a:", a.KeySegment);
        Assert.All(a.Collections.Values, c => Assert.Contains("tenant-a", c));
        var joined = string.Join(",", a.Collections.Values);
        Assert.DoesNotContain("tenant-b", joined);
        Assert.DoesNotContain("tenant-b", a.KeySegment);
    }

    [Fact]
    public void PurgeScope_CoversAllPersistingServices()
    {
        var scope = TenantPurgeScope.For("acme");
        foreach (var svc in TenantStorageScope.Services)
            Assert.True(scope.Collections.ContainsKey(svc), $"missing scope for {svc}");
    }

    // ── Orchestrator guards ───────────────────────────────────────────────────

    private sealed class FakePurger(string service, bool immutable) : ITenantStorePurger
    {
        public string Service => service;
        public bool IsImmutableEvidence => immutable;
        public TenantPurgeScope? Seen { get; private set; }
        public bool Ran { get; private set; }

        public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        {
            Ran = true;
            Seen = scope;
            return Task.FromResult(1);
        }
    }

    [Fact]
    public async Task Orchestrator_BlankTenant_Throws()
    {
        var orch = new TenantPurgeOrchestrator([new FakePurger("booking", false)]);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => orch.PurgeAsync("   ", sandboxReset: false));
    }

    [Fact]
    public async Task Orchestrator_SkipsImmutableEvidence_UnlessSandboxReset()
    {
        var booking = new FakePurger("booking", immutable: false);
        var audit = new FakePurger("audit", immutable: true);

        await new TenantPurgeOrchestrator([booking, audit]).PurgeAsync("acme", sandboxReset: false);
        Assert.True(booking.Ran);
        Assert.False(audit.Ran); // immutable evidence is never purged on a normal purge

        var audit2 = new FakePurger("audit", immutable: true);
        await new TenantPurgeOrchestrator([audit2]).PurgeAsync("acme", sandboxReset: true);
        Assert.True(audit2.Ran); // purged only on an explicit sandbox/demo reset
    }

    [Fact]
    public async Task Orchestrator_PassesTenantScopedScope_NotAnotherTenant()
    {
        var p = new FakePurger("booking", false);
        await new TenantPurgeOrchestrator([p]).PurgeAsync("tenant-a", sandboxReset: false);

        Assert.Equal("tenant-a", p.Seen!.TenantId);
        Assert.DoesNotContain("tenant-b", p.Seen.KeySegment);
    }
}
