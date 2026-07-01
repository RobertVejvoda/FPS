using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using FPS.SharedKernel.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Customer.Tests;

// PLAT003B — the nightly scheduler: disabled → no-op; the per-window lease means at most one replica
// resets per window (multiple replicas receive the same cron tick); and a misconfigured non-sandbox
// target is refused by the reset guard without any purge. Correctness under a first-run race is covered
// by the reset being idempotent, matching the repo's draw-scheduler strategy.
public sealed class ScheduledSandboxResetServiceTests
{
    private sealed class CountingPurger : ITenantStorePurger
    {
        private int ran;
        public int Ran => ran;
        public string Service => "spy";
        public bool IsImmutableEvidence => false;
        public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        {
            Interlocked.Increment(ref ran);
            return Task.FromResult(0);
        }
    }

    private sealed class FakeProfileClient : IDemoSeedProfileClient
    {
        public Task<(int profilesSeeded, string? error)> SeedAsync(string authorizationHeader, string tenantId, IReadOnlyList<DemoEmployeeRecord> employees, CancellationToken ct)
            => Task.FromResult((employees.Count, (string?)null));
    }

    private sealed class FakeConfigClient : IDemoSeedConfigurationClient
    {
        public Task<(int slotsSeeded, string? error)> SeedAsync(string authorizationHeader, string tenantId, string locationId, IReadOnlyList<DemoSlotRecord> slots, DemoPolicyRecord policy, CancellationToken ct)
            => Task.FromResult((slots.Count, (string?)null));
    }

    private sealed class NoopAudit : ISandboxResetAudit
    {
        public Task StartedAsync(string a, string t, CancellationToken ct) => Task.CompletedTask;
        public Task CompletedAsync(string a, string t, SandboxResetSummary s, CancellationToken ct) => Task.CompletedTask;
        public Task FailedAsync(string a, string t, string r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoopEvidence : ISandboxResetEvidenceStore
    {
        public Task RecordAsync(SandboxResetEvidence e, CancellationToken ct) => Task.CompletedTask;
        public Task<SandboxResetEvidence?> GetLatestAsync(string tenantId, CancellationToken ct) => Task.FromResult<SandboxResetEvidence?>(null);
    }

    // A lease that claims a window at most once — the shared instance models the distributed CAS so two
    // concurrent schedulers cannot both win the same window.
    private sealed class OnceLease : ISandboxResetLease
    {
        private readonly Lock gate = new();
        private string? claimed;
        public Task<bool> TryAcquireAsync(string window, CancellationToken ct)
        {
            lock (gate)
            {
                if (claimed == window) return Task.FromResult(false);
                claimed = window;
                return Task.FromResult(true);
            }
        }
    }

    private sealed class NeverLease : ISandboxResetLease
    {
        public Task<bool> TryAcquireAsync(string window, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset GetTenantUtcNow(string tenantId) => now;
    }

    private static TenantWorkspace Tenant(string id, TenantKind kind, bool resettable) => new()
    {
        TenantId = id, Slug = id, DisplayName = id, Region = "eu", TimeZone = "Europe/Prague",
        Kind = kind, IsResettableSandbox = resettable,
    };

    private static IConfiguration Config(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SandboxReset:Enabled"] = enabled ? "true" : "false" })
            .Build();

    private static SandboxResetService BuildReset(InMemoryTenantRepository repo, CountingPurger purger, bool enabled)
    {
        var seed = new TenantDemoSeedService(repo, new FakeProfileClient(), new FakeConfigClient());
        return new SandboxResetService(repo, new TenantPurgeOrchestrator([purger]), [purger], seed, new NoopAudit(), new NoopEvidence(), Config(enabled));
    }

    private static ScheduledSandboxResetService BuildScheduler(
        SandboxResetService reset, ISandboxResetLease lease, SandboxResetSchedulerOptions options) =>
        new(reset, lease, options, new FixedClock(new DateTimeOffset(2026, 7, 1, 2, 0, 0, TimeSpan.Zero)),
            NullLogger<ScheduledSandboxResetService>.Instance);

    [Fact]
    public async Task RunDueResets_Disabled_DoesNothing()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        var scheduler = BuildScheduler(BuildReset(repo, purger, enabled: true), new OnceLease(),
            new SandboxResetSchedulerOptions { Enabled = false });

        var outcomes = await scheduler.RunDueResetsAsync(default);

        Assert.Equal("Disabled", Assert.Single(outcomes).Status);
        Assert.Equal(0, purger.Ran);
    }

    [Fact]
    public async Task RunDueResets_WindowAlreadyClaimed_Skips()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        var scheduler = BuildScheduler(BuildReset(repo, purger, enabled: true), new NeverLease(),
            new SandboxResetSchedulerOptions { Enabled = true });

        var outcomes = await scheduler.RunDueResetsAsync(default);

        Assert.Equal("Skipped", Assert.Single(outcomes).Status);
        Assert.Equal(0, purger.Ran);
    }

    [Fact]
    public async Task RunDueResets_EnabledResettableTarget_ResetsOncePerTarget()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        var scheduler = BuildScheduler(BuildReset(repo, purger, enabled: true), new OnceLease(),
            new SandboxResetSchedulerOptions { Enabled = true, Targets = ["greenlogistics"] });

        var outcomes = await scheduler.RunDueResetsAsync(default);

        Assert.Equal("Succeeded", Assert.Single(outcomes).Status);
        Assert.Equal(1, purger.Ran);
    }

    [Fact]
    public async Task RunDueResets_ResetInert_ReportsUnavailableAndDoesNotPurge()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        // Scheduler on, but the reset itself not activated → safe no-op (inert).
        var scheduler = BuildScheduler(BuildReset(repo, purger, enabled: false), new OnceLease(),
            new SandboxResetSchedulerOptions { Enabled = true, Targets = ["greenlogistics"] });

        var outcomes = await scheduler.RunDueResetsAsync(default);

        Assert.Equal("Unavailable", Assert.Single(outcomes).Status);
        Assert.Equal(0, purger.Ran);
    }

    [Fact]
    public async Task RunDueResets_NonSandboxTarget_IsRefusedWithoutPurge()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        // A misconfigured target that is a real production tenant → guard refuses, no purge.
        await repo.SaveAsync(Tenant("acme", TenantKind.Production, resettable: false), default);
        var scheduler = BuildScheduler(BuildReset(repo, purger, enabled: true), new OnceLease(),
            new SandboxResetSchedulerOptions { Enabled = true, Targets = ["acme"] });

        var outcomes = await scheduler.RunDueResetsAsync(default);

        Assert.Equal("Refused", Assert.Single(outcomes).Status);
        Assert.Equal(0, purger.Ran);
    }

    [Fact]
    public async Task RunDueResets_TwoReplicasSameWindow_ResetsExactlyOnce()
    {
        var repo = new InMemoryTenantRepository();
        var purger = new CountingPurger();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        var reset = BuildReset(repo, purger, enabled: true);
        var sharedLease = new OnceLease();
        var options = new SandboxResetSchedulerOptions { Enabled = true, Targets = ["greenlogistics"] };
        var replicaA = BuildScheduler(reset, sharedLease, options);
        var replicaB = BuildScheduler(reset, sharedLease, options);

        var results = await Task.WhenAll(replicaA.RunDueResetsAsync(default), replicaB.RunDueResetsAsync(default));

        Assert.Equal(1, purger.Ran); // only the replica that won the window reset
        var statuses = results.Select(r => r.Single().Status).OrderBy(s => s).ToArray();
        Assert.Equal(["Skipped", "Succeeded"], statuses);
    }
}
