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
}
