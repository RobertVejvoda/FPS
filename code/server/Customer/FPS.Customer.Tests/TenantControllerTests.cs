using FPS.Customer.Application;
using FPS.Customer.Controllers;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Customer.Tests;

public sealed class TenantControllerTests
{
    private readonly InMemoryTenantRepository repository = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly TenantController controller;

    public TenantControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns("admin-1");
        controller = new TenantController(new TenantService(repository), currentUser.Object);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithTenantId()
    {
        var request = new CreateTenantRequest("beta-corp", "Beta Corp", "us-east", "America/New_York", []);

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<TenantResponse>(created.Value);
        Assert.Equal("beta-corp", response.Slug);
        Assert.Equal("Draft", response.LifecycleState);
        Assert.NotEmpty(response.TenantId);
    }

    [Fact]
    public async Task Create_NullSlug_Returns400NotThrows()
    {
        var result = await controller.Create(new CreateTenantRequest(null, "Corp", "eu", "UTC", []), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_DuplicateSlug_Returns400()
    {
        await controller.Create(new CreateTenantRequest("dup", "Dup", "eu", "UTC", []), CancellationToken.None);

        var result = await controller.Create(new CreateTenantRequest("dup", "Dup2", "eu", "UTC", []), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Get_ExistingTenant_Returns200()
    {
        var created = await controller.Create(new CreateTenantRequest("acme", "ACME", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.Get(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantResponse>(ok.Value);
        Assert.Equal(tenantId, response.TenantId);
    }

    [Fact]
    public async Task Get_UnknownTenant_Returns404()
    {
        var result = await controller.Get("no-such-id", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("me")]
    [InlineData("x")]
    [InlineData("fps-reserved")]
    public async Task Get_InvalidTenantId_Returns404NotThrows(string tenantId)
    {
        // Regression (#554): short/reserved strings like "me" reach this route via
        // /tenants/{tenantId} and previously threw ArgumentException from
        // CustomerStorageKey.Sanitise, producing a 500. Should return 404 instead.
        var result = await controller.Get(tenantId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Transition_ValidState_Returns204()
    {
        var created = await controller.Create(new CreateTenantRequest("xco", "XCo", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.Transition(tenantId, new TransitionRequest("Configured", "identity done", null), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Transition_InvalidState_Returns400()
    {
        var created = await controller.Create(new CreateTenantRequest("xco", "XCo", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.Transition(tenantId, new TransitionRequest("Ready", null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Transition_UnknownStateName_Returns400()
    {
        var result = await controller.Transition("any-id", new TransitionRequest("NonExistentState", null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetTransitions_ReturnsHistory()
    {
        var created = await controller.Create(new CreateTenantRequest("hist", "Hist", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await controller.Transition(tenantId, new TransitionRequest("Configured", "setup done", "evidence"), CancellationToken.None);

        var result = await controller.GetTransitions(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Update_ValidRequest_Returns204()
    {
        var created = await controller.Create(new CreateTenantRequest("upd", "Original", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.Update(tenantId,
            new UpdateTenantRequest("Updated Name", "Europe/Berlin",
                [new ContactDto("IT", "it@co.com", "support")]),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var tenant = await repository.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal("Updated Name", tenant!.DisplayName);
    }

    // ── TenantKind discriminator (#521 prerequisite) ─────────────────────────

    [Fact]
    public async Task Create_NoKindSpecified_DefaultsToProduction()
    {
        var result = await controller.Create(new CreateTenantRequest("prod-co", "Prod Co", "eu", "UTC", []), CancellationToken.None);

        var response = Assert.IsType<TenantResponse>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal("Production", response.Kind);
    }

    [Theory]
    [InlineData("Sandbox")]
    [InlineData("Evaluation")]
    [InlineData("Production")]
    public async Task Create_ExplicitKind_PersistedAndReturnedInResponse(string kind)
    {
        var slug = $"kind-{kind.ToLower()}";
        var result = await controller.Create(new CreateTenantRequest(slug, "Co", "eu", "UTC", [], Kind: kind), CancellationToken.None);

        var response = Assert.IsType<TenantResponse>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal(kind, response.Kind);
    }

    [Fact]
    public async Task Create_UnknownKind_Returns400()
    {
        var result = await controller.Create(new CreateTenantRequest("bad-kind", "Co", "eu", "UTC", [], Kind: "Enterprise"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Get_AfterSandboxCreate_ReturnsSandboxKind()
    {
        var created = await controller.Create(new CreateTenantRequest("sandbox-co", "Sandbox Co", "eu", "UTC", [], Kind: "Sandbox"), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.Get(tenantId, CancellationToken.None);

        var response = Assert.IsType<TenantResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Sandbox", response.Kind);
    }

    [Fact]
    public async Task Get_KindPersistedThroughSave_SurvivesRoundTrip()
    {
        // Verify that Kind is preserved after a save (e.g., following an update).
        var created = await controller.Create(new CreateTenantRequest("eval-co", "Eval Co", "eu", "UTC", [], Kind: "Evaluation"), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await controller.Update(tenantId, new UpdateTenantRequest("Eval Co Updated", "UTC", []), CancellationToken.None);

        var result = await controller.Get(tenantId, CancellationToken.None);

        var response = Assert.IsType<TenantResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Evaluation", response.Kind);
    }

    [Fact]
    public async Task Create_IncludesServiceCollectionsInResponse()
    {
        var request = new CreateTenantRequest("my-corp", "My Corp", "eu", "UTC", []);

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<TenantResponse>(created.Value);
        Assert.NotEmpty(response.ServiceCollections);
        Assert.Contains("booking", response.ServiceCollections.Keys);
        Assert.Contains("audit", response.ServiceCollections.Keys);
        Assert.All(response.ServiceCollections.Values, v => Assert.Contains("my-corp", v));
    }

    [Fact]
    public async Task GetProvisioning_ExistingTenant_ReturnsDeterministicCollectionNames()
    {
        var created = await controller.Create(new CreateTenantRequest("acme-co", "ACME Co", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.GetProvisioning(tenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var prov = Assert.IsType<ProvisioningResponse>(ok.Value);
        Assert.Equal("acme-co", prov.TenantSlug);
        Assert.Equal(tenantId, prov.TenantId);
        Assert.All(prov.ServiceCollections.Values, v =>
        {
            Assert.Contains("acme-co", v);
            Assert.DoesNotContain("password", v);
            Assert.DoesNotContain("secret", v);
        });
        Assert.Contains("booking", prov.ServiceCollections.Keys);
        Assert.Contains("notification", prov.ServiceCollections.Keys);
        Assert.Contains("profile", prov.ServiceCollections.Keys);
        Assert.Contains("audit", prov.ServiceCollections.Keys);
        Assert.Contains("configuration", prov.ServiceCollections.Keys);
        Assert.Contains("reporting", prov.ServiceCollections.Keys);
    }

    [Fact]
    public async Task GetProvisioning_UnknownTenant_Returns404()
    {
        var result = await controller.GetProvisioning("no-such-id", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetProvisioning_CollectionNamesDeriveFromSlugNotCallerId()
    {
        var result1 = await controller.Create(new CreateTenantRequest("slug-a", "Co A", "eu", "UTC", []), CancellationToken.None);
        var id1 = ((TenantResponse)((CreatedAtActionResult)result1).Value!).TenantId;

        var result2 = await controller.Create(new CreateTenantRequest("slug-b", "Co B", "eu", "UTC", []), CancellationToken.None);
        var id2 = ((TenantResponse)((CreatedAtActionResult)result2).Value!).TenantId;

        var prov1 = (ProvisioningResponse)((OkObjectResult)await controller.GetProvisioning(id1, CancellationToken.None)).Value!;
        var prov2 = (ProvisioningResponse)((OkObjectResult)await controller.GetProvisioning(id2, CancellationToken.None)).Value!;

        Assert.NotEqual(prov1.ServiceCollections["booking"], prov2.ServiceCollections["booking"]);
        Assert.Contains("slug-a", prov1.ServiceCollections["booking"]);
        Assert.Contains("slug-b", prov2.ServiceCollections["booking"]);
    }

    // ── Branding endpoints (AUTH002) ─────────────────────────────────────────

    [Fact]
    public async Task SetBranding_ValidRequest_Returns204()
    {
        var created = await controller.Create(new CreateTenantRequest("brand-ctrl", "Brand Ctrl", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.SetBranding(tenantId,
            new SetBrandingRequest("#aabbcc", null, null, null, null, "CompanySso"),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var tenant = await repository.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal("#aabbcc", tenant!.Branding.PrimaryColor);
    }

    [Fact]
    public async Task SetBranding_InvalidColor_Returns400()
    {
        var created = await controller.Create(new CreateTenantRequest("brand-bad", "Brand Bad", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.SetBranding(tenantId,
            new SetBrandingRequest("not-a-hex", null, null, null, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetBranding_UnknownTenant_Returns404()
    {
        var result = await controller.SetBranding("no-such",
            new SetBrandingRequest(null, null, null, null, null),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SetBranding_UnknownLoginMode_Returns400()
    {
        var created = await controller.Create(new CreateTenantRequest("brand-mode", "Brand Mode", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.SetBranding(tenantId,
            new SetBrandingRequest(null, null, null, null, null, "InvalidMode"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Discovery domain endpoints (AUTH002) ─────────────────────────────────

    [Fact]
    public async Task RegisterDiscoveryDomain_ValidDomain_Returns204()
    {
        var created = await controller.Create(new CreateTenantRequest("dom-ctrl", "Dom Ctrl", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.RegisterDiscoveryDomain(tenantId,
            new RegisterDiscoveryDomainRequest("dom-ctrl.example"),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RegisterDiscoveryDomain_UnknownTenant_Returns404()
    {
        var result = await controller.RegisterDiscoveryDomain("no-such",
            new RegisterDiscoveryDomainRequest("x.example"),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UnregisterDiscoveryDomain_ExistingDomain_Returns204()
    {
        var created = await controller.Create(new CreateTenantRequest("undom-ctrl", "UnDom", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await controller.RegisterDiscoveryDomain(tenantId, new RegisterDiscoveryDomainRequest("undom.example"), CancellationToken.None);

        var result = await controller.UnregisterDiscoveryDomain(tenantId, "undom.example", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UnregisterDiscoveryDomain_NonExistentDomain_Returns404()
    {
        var created = await controller.Create(new CreateTenantRequest("undom-miss", "UnDom Miss", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;

        var result = await controller.UnregisterDiscoveryDomain(tenantId, "missing.example", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}

public sealed class TenantDiscoveryControllerTests
{
    private readonly InMemoryTenantRepository repository = new();
    private readonly TenantService service;
    private readonly TenantDiscoveryController discoveryController;
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly TenantController tenantController;

    public TenantDiscoveryControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns("admin-1");
        service = new TenantService(repository);
        discoveryController = new TenantDiscoveryController(service);
        tenantController = new TenantController(service, currentUser.Object);
    }

    [Fact]
    public async Task Discover_RegisteredDomain_Returns200WithSafeFields()
    {
        var created = await tenantController.Create(new CreateTenantRequest("green-co", "Green Co", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await tenantController.SetBranding(tenantId, new SetBrandingRequest("#00ff00", null, null, null, null, "CompanySso"), CancellationToken.None);
        await tenantController.RegisterDiscoveryDomain(tenantId, new RegisterDiscoveryDomainRequest("green.example"), CancellationToken.None);

        var result = await discoveryController.Discover("green.example", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantDiscoveryResponse>(ok.Value);
        Assert.Equal("green-co", response.Slug);
        Assert.Equal("Green Co", response.DisplayName);
        Assert.Equal("CompanySso", response.LoginMode);
        Assert.Equal("#00ff00", response.PrimaryColor);
    }

    [Fact]
    public async Task Discover_UnregisteredDomain_Returns404()
    {
        var result = await discoveryController.Discover("unknown.example", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Discover_MissingDomainParam_Returns400()
    {
        var result = await discoveryController.Discover(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Discover_ResponseDoesNotContainTenantId()
    {
        var created = await tenantController.Create(new CreateTenantRequest("safe-disc", "Safe", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await tenantController.RegisterDiscoveryDomain(tenantId, new RegisterDiscoveryDomainRequest("safe.example"), CancellationToken.None);

        var result = await discoveryController.Discover("safe.example", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain(tenantId, json);
    }

    // ── Discovery: malformed and ambiguous inputs (AUTH005) ───────────────────

    [Theory]
    [InlineData("https://greenlogistics.example")]
    [InlineData("notadomain")]
    [InlineData("@greenlogistics.example")]
    [InlineData("../evil")]
    public async Task Discover_MalformedDomain_Returns404(string malformed)
    {
        var result = await discoveryController.Discover(malformed, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Discover_CaseInsensitiveDomain_Returns200()
    {
        var created = await tenantController.Create(new CreateTenantRequest("ci-co", "CI Co", "eu", "UTC", []), CancellationToken.None);
        var tenantId = ((TenantResponse)((CreatedAtActionResult)created).Value!).TenantId;
        await tenantController.RegisterDiscoveryDomain(tenantId, new RegisterDiscoveryDomainRequest("ci.example"), CancellationToken.None);

        var result = await discoveryController.Discover("CI.EXAMPLE", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }
}
