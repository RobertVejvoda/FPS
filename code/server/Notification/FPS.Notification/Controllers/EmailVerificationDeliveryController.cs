using FPS.Notification.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

/// <summary>
/// AUTH008B (#734) — internal (service-invocation only) endpoint Profile calls to deliver a FairSpot-local
/// email-verification message. The request body carries the verification link (Secret token embedded); it
/// is sent transiently and never persisted as a notification record. Dapr-internal only and excluded from
/// the public OpenAPI/web client.
/// </summary>
[ApiController]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class EmailVerificationDeliveryController(IVerificationEmailDelivery delivery) : ControllerBase
{
    [HttpPost("/internal/notification/email-verification")]
    public async Task<IActionResult> Send(
        [FromBody] VerificationEmailDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.EmailAddress) ||
            string.IsNullOrWhiteSpace(request.VerificationLink))
        {
            return BadRequest();
        }

        var sent = await delivery.SendAsync(
            new VerificationEmailRequest(request.TenantId, request.EmailAddress, request.VerificationLink), cancellationToken);

        // Body carries no link/token back — just the outcome.
        return sent ? Ok(new VerificationEmailDeliveryResult(true)) : StatusCode(StatusCodes.Status502BadGateway, new VerificationEmailDeliveryResult(false));
    }
}

public sealed record VerificationEmailDeliveryRequest(string TenantId, string EmailAddress, string VerificationLink);

public sealed record VerificationEmailDeliveryResult(bool Sent);
