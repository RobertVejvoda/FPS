using FPS.Notification.Application;
using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Notification.Tests;

// Exercises the wiring that the handler tests deliberately bypass: the
// seeder reads IConfiguration, populates the registered store, and the
// audience resolver returns those users. Codex review on PR #487 asked
// for this because the handler tests prove fan-out works "when seeded"
// but never confirm that the live app actually seeds.
public sealed class HrRosterConfigurationSeederTests
{
    [Fact]
    public async Task Seed_PopulatesRosterFromConfiguredSection_ResolverReturnsUsers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notification:HrRoster:demo:0"] = "hr-admin",
                ["Notification:HrRoster:demo:1"] = "hr-deputy",
                ["Notification:HrRoster:acme:0"] = "acme-hr-1",
            })
            .Build();

        var store = new InMemoryHrRosterStore();
        var resolver = new RosterBackedAudienceResolver(store);
        var seeder = new HrRosterConfigurationSeeder(
            config, store, NullLogger<HrRosterConfigurationSeeder>.Instance);

        seeder.Seed();

        var demoHr = await resolver.GetHrRecipientsAsync("demo");
        var acmeHr = await resolver.GetHrRecipientsAsync("acme");

        Assert.Contains("hr-admin", demoHr);
        Assert.Contains("hr-deputy", demoHr);
        Assert.Equal(2, demoHr.Count);
        Assert.Contains("acme-hr-1", acmeHr);
    }

    [Fact]
    public async Task Seed_NoConfigurationSection_RosterRemainsEmpty()
    {
        // Production-style default: when nothing is configured the seeder
        // must be a quiet no-op, not throw or pollute the roster.
        var config = new ConfigurationBuilder().Build();
        var store = new InMemoryHrRosterStore();
        var resolver = new RosterBackedAudienceResolver(store);
        var seeder = new HrRosterConfigurationSeeder(
            config, store, NullLogger<HrRosterConfigurationSeeder>.Instance);

        seeder.Seed();

        var hr = await resolver.GetHrRecipientsAsync("demo");
        Assert.Empty(hr);
    }

    [Fact]
    public async Task Seed_EmptyTenantArray_StoresEmptyRosterForTenant()
    {
        // Explicit empty list (e.g., tenant with no HR yet) must not throw
        // and must not silently inherit a previous roster.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notification:HrRoster:demo"] = null,
            })
            .Build();

        var store = new InMemoryHrRosterStore();
        var seeder = new HrRosterConfigurationSeeder(
            config, store, NullLogger<HrRosterConfigurationSeeder>.Instance);

        seeder.Seed();

        var hr = await new RosterBackedAudienceResolver(store).GetHrRecipientsAsync("demo");
        Assert.Empty(hr);
    }
}
