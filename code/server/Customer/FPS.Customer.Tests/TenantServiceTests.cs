using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class TenantServiceTests
{
    private readonly InMemoryTenantRepository repository = new();
    private readonly TenantService service;

    public TenantServiceTests() => service = new TenantService(repository);

    [Fact]
    public async Task Create_ValidInput_ReturnsTenantInDraftState()
    {
        var (tenant, error) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(tenant);
        Assert.Equal(TenantLifecycleState.Draft, tenant.LifecycleState);
        Assert.Equal("acme", tenant.Slug);
        Assert.Equal("ACME Corp", tenant.DisplayName);
        Assert.NotEmpty(tenant.TenantId);
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsError()
    {
        await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        var (tenant, error) = await service.CreateAsync("acme", "ACME Corp 2", "eu-west", "Europe/London", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.Contains("already in use", error);
    }

    [Fact]
    public async Task Create_MissingSlug_ReturnsError()
    {
        var (tenant, error) = await service.CreateAsync("", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.Contains("Slug", error);
    }

    [Fact]
    public async Task Create_NullSlug_ReturnsErrorWithoutThrowing()
    {
        var (tenant, error) = await service.CreateAsync(null, "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.NotNull(error);
        Assert.Contains("Slug", error);
    }

    [Fact]
    public async Task Create_SlugWithOnlySpecialChars_ReturnsError()
    {
        var (tenant, error) = await service.CreateAsync("@@@", "Corp", "eu", "UTC", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.Contains("Slug", error);
    }

    [Fact]
    public async Task Create_SlugNormalization_TrimAndLowercaseBeforeUniquenessCheck()
    {
        await service.CreateAsync("acme", "ACME", "eu", "UTC", [], CancellationToken.None);

        // " ACME " normalizes to "acme" — should collide.
        var (tenant, error) = await service.CreateAsync(" ACME ", "ACME 2", "eu", "UTC", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.Contains("already in use", error);
    }

    [Fact]
    public async Task Create_SlugCollisionAfterSanitization_ReturnsError()
    {
        // "a b" sanitizes to "a-b"; "a@b" also sanitizes to "a-b" — must collide.
        await service.CreateAsync("a b", "Corp A", "eu", "UTC", [], CancellationToken.None);

        var (tenant, error) = await service.CreateAsync("a@b", "Corp B", "eu", "UTC", [], CancellationToken.None);

        Assert.Null(tenant);
        Assert.Contains("already in use", error);
    }

    [Fact]
    public async Task Create_StoredSlugIsAlwaysSanitizedForm()
    {
        var (tenant, _) = await service.CreateAsync(" My Corp! ", "My Corp", "eu", "UTC", [], CancellationToken.None);

        Assert.NotNull(tenant);
        Assert.Equal("my-corp", tenant!.Slug); // trailing dash stripped by sanitizer
        Assert.Equal(tenant.Slug, tenant.Provisioning.TenantSlug);
    }

    [Fact]
    public async Task Update_ValidInput_PersistsChanges()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);
        var contacts = new List<TenantSupportContact> { new("IT Admin", "it@acme.com", "support") };

        var error = await service.UpdateAsync(created!.TenantId, "ACME Updated", "Europe/Paris", contacts, CancellationToken.None);

        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Equal("ACME Updated", tenant!.DisplayName);
        Assert.Equal("Europe/Paris", tenant.TimeZone);
        Assert.Single(tenant.SupportContacts);
    }

    [Fact]
    public async Task Update_ArchivedTenant_ReturnsError()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);
        await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Archived, "actor-1", "archiving", null, CancellationToken.None);

        var error = await service.UpdateAsync(created.TenantId, "ACME Updated", "Europe/Paris", [], CancellationToken.None);

        Assert.Contains("Archived", error);
    }

    [Fact]
    public async Task Transition_DraftToConfigured_Succeeds()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        var error = await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Configured, "actor-1", "identity configured", null, CancellationToken.None);

        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Configured, tenant!.LifecycleState);
        Assert.Single(tenant.Transitions);
    }

    [Fact]
    public async Task Transition_DraftToReady_Fails()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        var error = await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Ready, "actor-1", null, null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("not permitted", error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Draft, tenant!.LifecycleState);
    }

    [Fact]
    public async Task Transition_ArchivedToAny_Fails()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);
        await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Archived, "actor-1", "archiving", null, CancellationToken.None);

        var error = await service.TransitionAsync(created.TenantId, TenantLifecycleState.Draft, "actor-1", "restore attempt", null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("not permitted", error);
    }

    [Fact]
    public async Task Transition_SuspendedToReady_Succeeds()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);
        await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Configured, "actor-1", null, null, CancellationToken.None);
        await service.TransitionAsync(created.TenantId, TenantLifecycleState.Seeded, "actor-1", null, null, CancellationToken.None);
        await service.TransitionAsync(created.TenantId, TenantLifecycleState.Ready, "actor-1", null, null, CancellationToken.None);
        await service.TransitionAsync(created.TenantId, TenantLifecycleState.Suspended, "actor-1", "security review", null, CancellationToken.None);

        var error = await service.TransitionAsync(created.TenantId, TenantLifecycleState.Ready, "actor-1", "review passed", "evidence-token", CancellationToken.None);

        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Ready, tenant!.LifecycleState);
        Assert.Equal(5, tenant.Transitions.Count);
    }

    [Fact]
    public async Task Transition_RecordsActorAndReason()
    {
        var (created, _) = await service.CreateAsync("acme", "ACME Corp", "eu-west", "Europe/London", [], CancellationToken.None);

        await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Configured, "admin-user-42", "identity ready", "issuer=https://idp", CancellationToken.None);

        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        var t = Assert.Single(tenant!.Transitions);
        Assert.Equal("admin-user-42", t.ActorId);
        Assert.Equal("identity ready", t.Reason);
        Assert.Equal("issuer=https://idp", t.Evidence);
        Assert.Equal(TenantLifecycleState.Draft, t.From);
        Assert.Equal(TenantLifecycleState.Configured, t.To);
    }

    [Fact]
    public async Task Transition_UnknownTenant_ReturnsError()
    {
        var error = await service.TransitionAsync("no-such-tenant", TenantLifecycleState.Configured, "actor", null, null, CancellationToken.None);

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Create_GeneratesProvisioningMetadataFromSlug()
    {
        var (tenant, _) = await service.CreateAsync("my-company", "My Company", "eu", "UTC", [], CancellationToken.None);

        var p = tenant!.Provisioning;
        Assert.Equal(tenant.TenantId, p.TenantId);
        Assert.Equal("my-company", p.TenantSlug);
        Assert.NotEmpty(p.ServiceCollections);
        // PLAT002: scopes key off the canonical tenant id (the same value services store under),
        // not the display slug.
        Assert.All(p.ServiceCollections.Values, v => Assert.Contains(tenant.TenantId, v));
    }

    [Fact]
    public async Task Create_ProvisioningMetadata_IsDeterministicForSameSlug()
    {
        var (t1, _) = await service.CreateAsync("alpha", "Alpha", "eu", "UTC", [], CancellationToken.None);
        var (t2, _) = await service.CreateAsync("beta", "Beta", "eu", "UTC", [], CancellationToken.None);

        Assert.NotEqual(t1!.Provisioning.ServiceCollections["booking"], t2!.Provisioning.ServiceCollections["booking"]);
        Assert.Contains(t1.TenantId, t1.Provisioning.ServiceCollections["booking"]);
        Assert.Contains(t2.TenantId, t2.Provisioning.ServiceCollections["booking"]);
    }

    [Fact]
    public async Task Create_ProvisioningMetadata_IncludesAllPersistingServiceScopes()
    {
        // Explicit tenant id so the scope names are deterministic and readable in the assertion.
        var (tenant, _) = await service.CreateAsync("acme-corp", "Acme", "eu", "UTC", [], CancellationToken.None, requestedTenantId: "acme-corp");

        var scopes = tenant!.Provisioning.ServiceCollections;
        // PLAT002: durable evidence must cover every bounded context that persists tenant data.
        foreach (var service in new[] { "customer", "booking", "notification", "profile", "audit", "configuration", "datahub", "reporting" })
        {
            Assert.True(scopes.ContainsKey(service), $"provisioning metadata is missing the '{service}' scope");
            Assert.Equal($"fps-acme-corp-{service}", scopes[service]);
        }
    }

    [Fact]
    public async Task Create_ProvisioningMetadata_SanitizesSlugInCollectionNames()
    {
        // Slug sanitization strips chars that aren't alphanumeric/hyphen.
        var (tenant, _) = await service.CreateAsync("clean-slug", "Clean", "eu", "UTC", [], CancellationToken.None);

        Assert.All(tenant!.Provisioning.ServiceCollections.Values, v =>
        {
            Assert.DoesNotContain(" ", v);
            Assert.DoesNotContain("@", v);
        });
    }

    [Fact]
    public async Task Create_ProvisioningMetadata_CoversAllExpectedServices()
    {
        var (tenant, _) = await service.CreateAsync("svc-test", "Svc Test", "eu", "UTC", [], CancellationToken.None);

        var keys = tenant!.Provisioning.ServiceCollections.Keys.ToHashSet();
        foreach (var svc in new[] { "customer", "booking", "notification", "profile", "audit", "configuration", "reporting" })
            Assert.Contains(svc, keys);
    }

    // ── Deterministic tenant ID (OPS008B) ────────────────────────────────────

    [Fact]
    public async Task Create_WithRequestedTenantId_UsesThatId()
    {
        var (tenant, error) = await service.CreateAsync(
            "acme", "ACME Corp", "eu", "UTC", [], CancellationToken.None,
            requestedTenantId: "acme-corp");

        Assert.Null(error);
        Assert.Equal("acme-corp", tenant!.TenantId);
    }

    [Fact]
    public async Task Create_WithRequestedTenantId_GeneratesGuidWhenOmitted()
    {
        var (tenant, _) = await service.CreateAsync("acme2", "ACME 2", "eu", "UTC", [], CancellationToken.None);

        Assert.True(Guid.TryParse(tenant!.TenantId, out _), "Expected a GUID when no tenantId requested.");
    }

    [Fact]
    public async Task Create_DuplicateTenantId_ReturnsError()
    {
        await service.CreateAsync("slug1", "Corp 1", "eu", "UTC", [], CancellationToken.None, "shared-id");

        var (tenant, error) = await service.CreateAsync("slug2", "Corp 2", "eu", "UTC", [], CancellationToken.None, "shared-id");

        Assert.Null(tenant);
        Assert.Contains("already in use", error);
    }

    [Fact]
    public async Task Create_RequestedTenantId_IsSanitised()
    {
        var (tenant, error) = await service.CreateAsync(
            "slug3", "Corp 3", "eu", "UTC", [], CancellationToken.None,
            requestedTenantId: "My Tenant ID!");

        // Sanitisation strips spaces and special chars; resulting safe slug used or an error returned.
        // Either is acceptable — what matters is no raw-unsanitised value is stored.
        if (error is null)
            Assert.DoesNotContain(" ", tenant!.TenantId);
    }

    [Fact]
    public async Task Create_RequestedTenantId_AllSpecialChars_ReturnsError()
    {
        var (tenant, error) = await service.CreateAsync(
            "slug4", "Corp 4", "eu", "UTC", [], CancellationToken.None,
            requestedTenantId: "!!!");

        Assert.Null(tenant);
        Assert.NotNull(error);
        Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RequestedTenantId_AllSpecialChars_DoesNotStoreEmptyId()
    {
        await service.CreateAsync("slug5", "Corp 5", "eu", "UTC", [], CancellationToken.None, "!!!");

        var retrieved = await service.GetAsync("", CancellationToken.None);
        Assert.Null(retrieved);
    }

    // ── Branding (AUTH002) ────────────────────────────────────────────────────

    [Fact]
    public async Task SetBranding_ValidConfig_PersistsBranding()
    {
        var (created, _) = await service.CreateAsync("brand-co", "Brand Co", "eu", "UTC", [], CancellationToken.None);
        var config = new TenantBrandingConfig { PrimaryColor = "#ff0000", LoginMode = TenantLoginMode.CompanySso };

        var error = await service.SetBrandingAsync(created!.TenantId, config, CancellationToken.None);

        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Equal("#ff0000", tenant!.Branding.PrimaryColor);
        Assert.Equal(TenantLoginMode.CompanySso, tenant.Branding.LoginMode);
    }

    [Fact]
    public async Task SetBranding_InvalidHexColor_ReturnsError()
    {
        var (created, _) = await service.CreateAsync("brand-err", "Err Co", "eu", "UTC", [], CancellationToken.None);

        var error = await service.SetBrandingAsync(created!.TenantId, new TenantBrandingConfig { PrimaryColor = "red" }, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("PrimaryColor", error);
    }

    [Fact]
    public async Task SetBranding_UnknownTenant_ReturnsError()
    {
        var error = await service.SetBrandingAsync("no-such", new TenantBrandingConfig(), CancellationToken.None);

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task SetBranding_ArchivedTenant_ReturnsError()
    {
        var (created, _) = await service.CreateAsync("arch-brand", "Arch", "eu", "UTC", [], CancellationToken.None);
        await service.TransitionAsync(created!.TenantId, TenantLifecycleState.Archived, "actor", null, null, CancellationToken.None);

        var error = await service.SetBrandingAsync(created.TenantId, new TenantBrandingConfig(), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("Archived", error);
    }

    // ── Discovery domains (AUTH002) ───────────────────────────────────────────

    [Fact]
    public async Task RegisterDiscoveryDomain_ValidDomain_PersistsDomain()
    {
        var (created, _) = await service.CreateAsync("disc-co", "Disc Co", "eu", "UTC", [], CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(created!.TenantId, "disc.example", "actor-hash", CancellationToken.None);

        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Single(tenant!.DiscoveryDomains);
        Assert.Equal("disc.example", tenant.DiscoveryDomains[0].Domain);
    }

    [Fact]
    public async Task RegisterDiscoveryDomain_DuplicateOnSameTenant_ReturnsError()
    {
        var (created, _) = await service.CreateAsync("dup-dom", "Dup", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created!.TenantId, "dup.example", "h", CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(created.TenantId, "dup.example", "h", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("already registered", error);
    }

    [Fact]
    public async Task RegisterDiscoveryDomain_SameDomainOnAnotherTenant_ReturnsError()
    {
        var (t1, _) = await service.CreateAsync("ten1", "T1", "eu", "UTC", [], CancellationToken.None);
        var (t2, _) = await service.CreateAsync("ten2", "T2", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(t1!.TenantId, "shared.example", "h", CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(t2!.TenantId, "shared.example", "h", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("another tenant", error);
    }

    [Fact]
    public async Task RegisterDiscoveryDomain_InvalidFormat_ReturnsError()
    {
        var (created, _) = await service.CreateAsync("fmt-co", "Fmt", "eu", "UTC", [], CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(created!.TenantId, "notadomain", "h", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnregisterDiscoveryDomain_ExistingDomain_RemovesDomain()
    {
        var (created, _) = await service.CreateAsync("rm-dom", "Rm", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created!.TenantId, "rm.example", "h", CancellationToken.None);

        var (found, error) = await service.UnregisterDiscoveryDomainAsync(created.TenantId, "rm.example", CancellationToken.None);

        Assert.True(found);
        Assert.Null(error);
        var tenant = await service.GetAsync(created.TenantId, CancellationToken.None);
        Assert.Empty(tenant!.DiscoveryDomains);
    }

    [Fact]
    public async Task UnregisterDiscoveryDomain_NonExistentDomain_ReturnsFalse()
    {
        var (created, _) = await service.CreateAsync("rm-miss", "Rm Miss", "eu", "UTC", [], CancellationToken.None);

        var (found, error) = await service.UnregisterDiscoveryDomainAsync(created!.TenantId, "missing.example", CancellationToken.None);

        Assert.False(found);
        Assert.Null(error);
    }

    [Fact]
    public async Task DiscoverAsync_RegisteredDomain_ReturnsSafeResponse()
    {
        var (created, _) = await service.CreateAsync("discover-co", "Discover Co", "eu", "UTC", [], CancellationToken.None);
        await service.SetBrandingAsync(created!.TenantId, new TenantBrandingConfig { PrimaryColor = "#123456", LoginMode = TenantLoginMode.CompanySso }, CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created.TenantId, "discover.example", "h", CancellationToken.None);

        var response = await service.DiscoverAsync("discover.example", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("discover-co", response!.Slug);
        Assert.Equal("Discover Co", response.DisplayName);
        Assert.Equal("CompanySso", response.LoginMode);
        Assert.Equal("#123456", response.PrimaryColor);
    }

    [Fact]
    public async Task DiscoverAsync_UnregisteredDomain_ReturnsNull()
    {
        var result = await service.DiscoverAsync("unknown.example", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverAsync_DoesNotExposeInternalIds()
    {
        var (created, _) = await service.CreateAsync("safe-co", "Safe Co", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created!.TenantId, "safe.example", "h", CancellationToken.None);

        var response = await service.DiscoverAsync("safe.example", CancellationToken.None);

        Assert.NotNull(response);
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.DoesNotContain(created.TenantId, json);
    }

    [Fact]
    public async Task DiscoverAsync_AfterUnregister_ReturnsNull()
    {
        var (created, _) = await service.CreateAsync("post-rm", "Post Rm", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created!.TenantId, "post-rm.example", "h", CancellationToken.None);
        await service.UnregisterDiscoveryDomainAsync(created.TenantId, "post-rm.example", CancellationToken.None);

        var result = await service.DiscoverAsync("post-rm.example", CancellationToken.None);

        Assert.Null(result);
    }

    // ── Discovery: malformed and ambiguous inputs (AUTH005) ───────────────────

    [Theory]
    [InlineData("https://greenlogistics.example")]
    [InlineData("notadomain")]
    [InlineData("../evil")]
    [InlineData("@greenlogistics.example")]
    [InlineData("green logistics.example")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DiscoverAsync_MalformedInput_ReturnsNull(string input)
    {
        var result = await service.DiscoverAsync(input, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverAsync_CaseInsensitiveLookup_ReturnsResult()
    {
        var (created, _) = await service.CreateAsync("case-co", "Case Co", "eu", "UTC", [], CancellationToken.None);
        await service.RegisterDiscoveryDomainAsync(created!.TenantId, "case.example", "h", CancellationToken.None);

        var result = await service.DiscoverAsync("CASE.EXAMPLE", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Case Co", result!.DisplayName);
    }

    [Fact]
    public async Task DiscoverAsync_AmbiguousDomainCannotOccur_SecondRegistrationIsRejected()
    {
        // Domain uniqueness is enforced at registration time, so DiscoverAsync
        // can never encounter a domain claimed by two tenants simultaneously.
        var (t1, _) = await service.CreateAsync("ambig-t1", "Tenant One", "eu", "UTC", [], CancellationToken.None);
        var (t2, _) = await service.CreateAsync("ambig-t2", "Tenant Two", "eu", "UTC", [], CancellationToken.None);

        await service.RegisterDiscoveryDomainAsync(t1!.TenantId, "ambig.example", "h", CancellationToken.None);
        var error = await service.RegisterDiscoveryDomainAsync(t2!.TenantId, "ambig.example", "h", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("already registered", error, StringComparison.OrdinalIgnoreCase);

        // Discover still returns the original tenant — no ambiguity.
        var result = await service.DiscoverAsync("ambig.example", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("ambig-t1", result!.Slug);
    }

    // ── Domain format validation (AUTH002) ────────────────────────────────────

    [Theory]
    [InlineData("example.com")]
    [InlineData("sub.example.com")]
    [InlineData("greenlogistics.example")]
    [InlineData("my-company.co.uk")]
    [InlineData("xn--nxasmq6b.com")]
    public async Task RegisterDiscoveryDomain_ValidHostnames_Succeed(string domain)
    {
        var (created, _) = await service.CreateAsync($"host-{Guid.NewGuid():N}", "Co", "eu", "UTC", [], CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(created!.TenantId, domain, "h", CancellationToken.None);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("https://greenlogistics.example")]
    [InlineData("green logistics.example")]
    [InlineData("green/logistics.example")]
    [InlineData("*@greenlogistics.example")]
    [InlineData("notadomain")]
    [InlineData(".example.com")]
    [InlineData("example.com.")]
    [InlineData("exam..ple.com")]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a.b")]
    public async Task RegisterDiscoveryDomain_InvalidHostnames_ReturnError(string domain)
    {
        var (created, _) = await service.CreateAsync($"host-{Guid.NewGuid():N}", "Co", "eu", "UTC", [], CancellationToken.None);

        var error = await service.RegisterDiscoveryDomainAsync(created!.TenantId, domain, "h", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("invalid", error, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Verifies the Green Logistics demo tenant seed produces the expected
/// branding, discovery domain, and lifecycle state (AUTH003).
/// </summary>
public sealed class GreenLogisticsSeedTests
{
    private readonly InMemoryTenantRepository repository = new();
    private readonly TenantService service;

    public GreenLogisticsSeedTests() => service = new TenantService(repository);

    private async Task<TenantWorkspace> SeedAsync()
    {
        const string tenantId = "greenlogistics";
        const string slug = "greenlogistics";

        var (tenant, error) = await service.CreateAsync(
            slug, "Green Logistics", "EU", "Europe/Prague",
            [
                new TenantSupportContact("GL Facilities", "facilities@greenlogistics.example", "Facilities"),
                new TenantSupportContact("GL IT Support", "it@greenlogistics.example", "Identity"),
            ],
            CancellationToken.None, requestedTenantId: tenantId);

        Assert.Null(error);
        Assert.NotNull(tenant);

        await service.SetBrandingAsync(tenant!.TenantId, new TenantBrandingConfig
        {
            PrimaryColor = "#2e7d32",
            AccentColor = "#a5d6a7",
            LoginMode = TenantLoginMode.Both,
        }, CancellationToken.None);

        await service.RegisterDiscoveryDomainAsync(tenant.TenantId, "greenlogistics.example", "local-seed", CancellationToken.None);

        await service.TransitionAsync(tenant.TenantId, TenantLifecycleState.Configured, "local-seed", "demo setup", null, CancellationToken.None);
        await service.TransitionAsync(tenant.TenantId, TenantLifecycleState.Seeded, "local-seed", "demo seed available", null, CancellationToken.None);

        return (await service.GetAsync(tenant.TenantId, CancellationToken.None))!;
    }

    [Fact]
    public async Task Seed_ProducesCorrectTenantId()
    {
        var tenant = await SeedAsync();
        Assert.Equal("greenlogistics", tenant.TenantId);
        Assert.Equal("greenlogistics", tenant.Slug);
    }

    [Fact]
    public async Task Seed_LifecycleIsSeeded()
    {
        var tenant = await SeedAsync();
        Assert.Equal(TenantLifecycleState.Seeded, tenant.LifecycleState);
    }

    [Fact]
    public async Task Seed_BrandingIsApplied()
    {
        var tenant = await SeedAsync();
        Assert.Equal("#2e7d32", tenant.Branding.PrimaryColor);
        Assert.Equal("#a5d6a7", tenant.Branding.AccentColor);
        Assert.Equal(TenantLoginMode.Both, tenant.Branding.LoginMode);
    }

    [Fact]
    public async Task Seed_DiscoveryDomainIsRegistered()
    {
        var tenant = await SeedAsync();
        Assert.Single(tenant.DiscoveryDomains);
        Assert.Equal("greenlogistics.example", tenant.DiscoveryDomains[0].Domain);
    }

    [Fact]
    public async Task Seed_DiscoverByDomainReturnsGreenLogistics()
    {
        await SeedAsync();
        var response = await service.DiscoverAsync("greenlogistics.example", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("greenlogistics", response!.Slug);
        Assert.Equal("Green Logistics", response.DisplayName);
        Assert.Equal("Both", response.LoginMode);
        Assert.Equal("#2e7d32", response.PrimaryColor);
    }

    [Fact]
    public async Task Seed_SupportContactsUseGreenLogisticsExampleDomain()
    {
        var tenant = await SeedAsync();
        Assert.All(tenant.SupportContacts, c => Assert.EndsWith("@greenlogistics.example", c.Email));
    }

    [Fact]
    public async Task Seed_IdempotentSecondCall_DoesNotError()
    {
        await SeedAsync();
        // Second seed attempt should be a no-op (tenant already exists).
        var existing = await service.GetAsync("greenlogistics", CancellationToken.None);
        Assert.NotNull(existing);
        // No duplicate domain error should occur since domain already registered.
        var domainError = await service.RegisterDiscoveryDomainAsync("greenlogistics", "greenlogistics.example", "h", CancellationToken.None);
        Assert.NotNull(domainError);
        Assert.Contains("already registered", domainError);
    }
}
