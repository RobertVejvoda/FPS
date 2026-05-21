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
        Assert.All(p.ServiceCollections.Values, v => Assert.Contains("my-company", v));
    }

    [Fact]
    public async Task Create_ProvisioningMetadata_IsDeterministicForSameSlug()
    {
        var (t1, _) = await service.CreateAsync("alpha", "Alpha", "eu", "UTC", [], CancellationToken.None);
        var (t2, _) = await service.CreateAsync("beta", "Beta", "eu", "UTC", [], CancellationToken.None);

        Assert.NotEqual(t1!.Provisioning.ServiceCollections["booking"], t2!.Provisioning.ServiceCollections["booking"]);
        Assert.Contains("alpha", t1.Provisioning.ServiceCollections["booking"]);
        Assert.Contains("beta", t2.Provisioning.ServiceCollections["booking"]);
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
}
