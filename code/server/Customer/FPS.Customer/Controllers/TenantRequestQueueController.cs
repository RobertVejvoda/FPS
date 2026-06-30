using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT004 — platform-operator triage of tenant requests. The queue holds cross-tenant prospect
/// PII, so it is platform-plane only (<see cref="RequirePlatformOperatorAttribute"/> from PLAT001):
/// platform operators triage, platform admins are a superset, and a tenant admin can never reach
/// it. Approve/Reject advance the request; provisioning stays a separate, later step.
///
/// Excluded from the open OpenAPI document (ApiExplorerSettings.IgnoreApi) so the generated
/// open <c>@fps/api-client</c> does not expose this platform-plane queue (#673); the public
/// intake <c>POST /tenant-requests</c> (TenantRequestIntakeController) stays in the open client.
/// </summary>
[ApiController]
[RequirePlatformOperator]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class TenantRequestQueueController(TenantRequestService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/tenant-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost("/tenant-requests/{requestId}/approve")]
    public Task<IActionResult> Approve(string requestId, [FromBody] TenantRequestDecision? body, CancellationToken ct)
        => Decide(service.ApproveAsync(requestId, Hash(currentUser.UserId), body?.Reason, ct));

    [HttpPost("/tenant-requests/{requestId}/reject")]
    public Task<IActionResult> Reject(string requestId, [FromBody] TenantRequestDecision? body, CancellationToken ct)
        => Decide(service.RejectAsync(requestId, Hash(currentUser.UserId), body?.Reason, ct));

    private static async Task<IActionResult> Decide(Task<(TenantRequest? request, string? error)> operation)
    {
        var (request, error) = await operation;
        if (error is null) return new OkObjectResult(request);
        return error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? new NotFoundObjectResult(new { error })
            : new BadRequestObjectResult(new { error });
    }

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}

public sealed record TenantRequestDecision(string? Reason);
