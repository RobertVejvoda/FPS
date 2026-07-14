using FPS.Customer.Domain;

namespace FPS.Customer.Application;

/// <summary>
/// #719 — heals an already-seeded Green Logistics sandbox tenant so its stored state matches the
/// current fresh-creation seed. Three repairs, all scoped to sandbox tenants and all idempotent:
/// <list type="bullet">
/// <item>PLAT003A — restore the resettable sandbox marker when local/harness state predates it.</item>
/// <item>#710 / #719 — enable the Seats module when the tenant was seeded before module selection
/// existed (it restores as Parking-only, so Booking reads no Seats over Dapr and rejects seat
/// requests with <c>422 RequestorIneligible</c>).</item>
/// <item>LOC001 (#744) — backfill the Czech default locale when the tenant was seeded before
/// DefaultLocale existed (it restores as null).</item>
/// </list>
/// Returns the healed workspace and whether anything changed; the seeder only persists on change.
/// The caller is Development-gated, so this only ever touches local/harness state — a hosted GL
/// tenant provisioned pre-#710 needs the platform module flow instead.
/// </summary>
internal static class GreenLogisticsSeedHealer
{
    public static (TenantWorkspace Tenant, bool Changed) Heal(TenantWorkspace existing)
    {
        var changed = false;

        // PLAT003A repair: upgrade to the resettable sandbox marker so it satisfies the reset
        // guard. IsResettableSandbox is init-only, so Restore is the only way to flip it. Never
        // touches a non-sandbox tenant.
        if (existing.Kind == TenantKind.Sandbox && !existing.IsResettableSandbox)
        {
            existing = TenantWorkspace.Restore(
                existing.TenantId, existing.Slug, existing.DisplayName, existing.Region, existing.TimeZone,
                existing.DefaultLocale,
                existing.SupportContacts, existing.Kind, isResettableSandbox: true, existing.LifecycleState,
                existing.Transitions, existing.Provisioning, existing.Branding, existing.DiscoveryDomains,
                existing.SeedEvents, existing.CreatedAt, existing.UpdatedAt,
                existing.PrimaryModule, existing.EnabledModules);
            changed = true;
        }

        // #719 repair: enable Seats in place to match the fresh-creation path — Parking primary with
        // Seats enabled. Scoped to sandbox so it never re-enables a module on a real tenant.
        if (existing.Kind == TenantKind.Sandbox && !existing.EnabledModules.Contains(TenantModule.Seats))
        {
            existing.SetModules(TenantModule.Parking, [TenantModule.Parking, TenantModule.Seats]);
            changed = true;
        }

        // LOC001 repair: backfill the Czech showcase locale in place. DefaultLocale is a plain
        // settable property (like TimeZone), so no Restore round-trip is needed. Scoped to sandbox
        // so it never sets a locale on a real tenant.
        if (existing.Kind == TenantKind.Sandbox && existing.DefaultLocale is null)
        {
            existing.DefaultLocale = "cs-CZ";
            changed = true;
        }

        return (existing, changed);
    }
}
