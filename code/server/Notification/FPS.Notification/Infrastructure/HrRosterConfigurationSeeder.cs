using FPS.Notification.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FPS.Notification.Infrastructure;

// Seeds IHrRosterStore from configuration on startup. Without this the
// in-memory store stays empty and HR fan-out never fires in the live
// demo/customer flow, even though the handler is wired correctly (Codex
// review on PR #487).
//
// Configuration shape: a section "Notification:HrRoster" mapping each
// tenant id to its list of HR user ids, e.g.
//   "Notification": { "HrRoster": { "demo": ["hr-admin"] } }
//
// Production overrides this via the usual ASP.NET configuration chain
// (env vars, secrets, etc.). A future slice replaces this with an
// identity-event-fed roster.
public sealed class HrRosterConfigurationSeeder(
    IConfiguration configuration,
    IHrRosterStore store,
    ILogger<HrRosterConfigurationSeeder> logger)
{
    public const string ConfigSection = "Notification:HrRoster";

    public void Seed()
    {
        var section = configuration.GetSection(ConfigSection);
        if (!section.Exists())
        {
            logger.LogInformation("No HR roster configuration found at {Section}; roster will start empty.", ConfigSection);
            return;
        }

        var tenantCount = 0;
        var userCount = 0;
        foreach (var tenantSection in section.GetChildren())
        {
            var tenantId = tenantSection.Key;
            var users = tenantSection.Get<string[]>() ?? Array.Empty<string>();
            store.Set(tenantId, users);
            if (users.Length > 0)
            {
                tenantCount++;
                userCount += users.Length;
            }
        }

        logger.LogInformation(
            "HR roster seeded from configuration. Tenants={TenantCount} Users={UserCount}",
            tenantCount, userCount);
    }
}
