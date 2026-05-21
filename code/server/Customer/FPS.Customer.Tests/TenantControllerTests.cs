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
}
