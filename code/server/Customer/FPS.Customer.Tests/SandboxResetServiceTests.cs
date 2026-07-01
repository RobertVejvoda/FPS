using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace FPS.Customer.Tests;

// PLAT003A — the safety-critical guard: a reset must abort BEFORE any purge for a missing/unknown
// tenant or a tenant that is not a resettable sandbox, and the sandbox status is read from stored
// metadata (never the request). Only Kind==Sandbox AND IsResettableSandbox may be reset.
public sealed class SandboxResetServiceTests
{
    private sealed class SpyPurger : ITenantStorePurger
    {
        public bool Ran { get; private set; }
        public string Service => "spy";
        public bool IsImmutableEvidence => false;
        public Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
        {
            Ran = true;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeProfileClient : IDemoSeedProfileClient
    {
        public Task<(int profilesSeeded, string? error)> SeedAsync(string authorizationHeader, IReadOnlyList<DemoEmployeeRecord> employees, CancellationToken ct)
            => Task.FromResult((employees.Count, (string?)null));
    }

    private sealed class FakeConfigClient : IDemoSeedConfigurationClient
    {
        public Task<(int slotsSeeded, string? error)> SeedAsync(string authorizationHeader, string locationId, IReadOnlyList<DemoSlotRecord> slots, DemoPolicyRecord policy, CancellationToken ct)
            => Task.FromResult((slots.Count, (string?)null));
    }

    private sealed class SpyAudit : ISandboxResetAudit
    {
        public bool Started { get; private set; }
        public bool Completed { get; private set; }
        public bool Failed { get; private set; }
        public Task StartedAsync(string a, string t, CancellationToken ct) { Started = true; return Task.CompletedTask; }
        public Task CompletedAsync(string a, string t, SandboxResetSummary s, CancellationToken ct) { Completed = true; return Task.CompletedTask; }
        public Task FailedAsync(string a, string t, string r, CancellationToken ct) { Failed = true; return Task.CompletedTask; }
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

    private static (SandboxResetService svc, SpyPurger purger, SpyAudit audit, InMemoryTenantRepository repo) Build(bool enabled = true)
    {
        var repo = new InMemoryTenantRepository();
        var purger = new SpyPurger();
        var audit = new SpyAudit();
        var seed = new TenantDemoSeedService(repo, new FakeProfileClient(), new FakeConfigClient());
        var svc = new SandboxResetService(repo, new TenantPurgeOrchestrator([purger]), [purger], seed, audit, Config(enabled));
        return (svc, purger, audit, repo);
    }

    [Fact]
    public async Task Reset_NonSandboxTenant_AbortsBeforePurge()
    {
        var (svc, purger, audit, repo) = Build();
        await repo.SaveAsync(Tenant("acme", TenantKind.Production, resettable: false), default);

        var (summary, error) = await svc.ResetAsync("acme", "actor", "Bearer x", default);

        Assert.Null(summary);
        Assert.Contains("not a resettable sandbox", error);
        Assert.False(purger.Ran, "purge must not run for a non-sandbox tenant");
        Assert.False(audit.Started, "no reset should even start for a non-sandbox tenant");
    }

    [Fact]
    public async Task Reset_SandboxKindButNotFlagged_AbortsBeforePurge()
    {
        var (svc, purger, _, repo) = Build();
        // Kind==Sandbox but the durable resettable flag is false → still refused (defence in depth).
        await repo.SaveAsync(Tenant("shadow", TenantKind.Sandbox, resettable: false), default);

        var (_, error) = await svc.ResetAsync("shadow", "actor", "Bearer x", default);

        Assert.Contains("not a resettable sandbox", error);
        Assert.False(purger.Ran);
    }

    [Fact]
    public async Task Reset_UnknownTenant_AbortsBeforePurge()
    {
        var (svc, purger, _, _) = Build();
        var (_, error) = await svc.ResetAsync("nope", "actor", "Bearer x", default);
        Assert.Contains("Unknown", error);
        Assert.False(purger.Ran);
    }

    [Fact]
    public async Task Reset_ResettableSandbox_PurgesThenReseedsAndAudits()
    {
        var (svc, purger, audit, repo) = Build();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);

        var (summary, error) = await svc.ResetAsync("greenlogistics", "actor", "Bearer x", default);

        Assert.Null(error);
        Assert.NotNull(summary);
        Assert.True(purger.Ran, "purge should run for a resettable sandbox");
        Assert.True(audit.Started && audit.Completed);
        Assert.True(summary!.ProfilesSeeded > 0 && summary.SlotsSeeded > 0);
    }

    [Fact]
    public async Task Reset_ResettableSandbox_NoPurgersRegistered_FailsClosed()
    {
        var repo = new InMemoryTenantRepository();
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);
        var audit = new SpyAudit();
        var seed = new TenantDemoSeedService(repo, new FakeProfileClient(), new FakeConfigClient());
        // No purgers registered → must not purge/reseed and must not report a fake success.
        var svc = new SandboxResetService(repo, new TenantPurgeOrchestrator([]), [], seed, audit, Config(enabled: true));

        var (summary, error) = await svc.ResetAsync("greenlogistics", "actor", "Bearer x", default);

        Assert.Null(summary);
        Assert.StartsWith("unavailable", error);
        Assert.False(audit.Started, "a fail-closed reset must not start or audit a destructive run");
    }

    [Fact]
    public async Task Reset_PurgerRegisteredButNotExplicitlyEnabled_FailsClosed()
    {
        // A registered purger must NOT activate the destructive path without the explicit opt-in.
        var (svc, purger, audit, repo) = Build(enabled: false);
        await repo.SaveAsync(Tenant("greenlogistics", TenantKind.Sandbox, resettable: true), default);

        var (summary, error) = await svc.ResetAsync("greenlogistics", "actor", "Bearer x", default);

        Assert.Null(summary);
        Assert.StartsWith("unavailable", error);
        Assert.False(purger.Ran, "adding a purger must not enable the reset without SandboxReset:Enabled");
        Assert.False(audit.Started);
    }
}
