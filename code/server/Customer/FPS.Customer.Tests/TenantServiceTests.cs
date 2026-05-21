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
}
