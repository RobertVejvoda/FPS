using FPS.Booking.Application.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

/// <summary>
/// Service-owned erasure endpoint called by the privacy workflow via Dapr service invocation.
/// Internal only — not exposed publicly.
/// </summary>
[ApiController]
public sealed class ErasureController(IBookingRepository bookingRepository) : ControllerBase
{
    [HttpPost("/erasure/check-active")]
    public async Task<IActionResult> CheckActive([FromBody] ServiceErasureInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.TargetUserId))
            return Ok(new ServiceErasureResult("booking-check", ErasureTreatment.NotApplicable, 0));

        var hasActive = await bookingRepository.HasActiveRequestsForRequestorAsync(
            input.TenantId, input.TargetUserId, ct);

        return hasActive
            ? Ok(new ServiceErasureResult("booking-check", ErasureTreatment.Blocked, 1,
                "Active booking(s) must be resolved before erasure."))
            : Ok(new ServiceErasureResult("booking-check", ErasureTreatment.NotApplicable, 0));
    }

    [HttpPost("/erasure")]
    public async Task<IActionResult> Erase([FromBody] ServiceErasureInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.TargetUserId))
            return Ok(new ServiceErasureResult("booking", ErasureTreatment.NotApplicable, 0));

        var count = await bookingRepository.AnonymiseByRequestorIdAsync(
            input.TenantId, input.TargetUserId, ct);

        return Ok(new ServiceErasureResult("booking",
            count > 0 ? ErasureTreatment.Anonymised : ErasureTreatment.NotApplicable, count));
    }
}

// Shared input/result types for service erasure endpoints (mirrors ErasureModels in FPS.Audit)
public sealed record ServiceErasureInput(
    string ErasureRequestId,
    string TenantId,
    string TargetActorHash,
    string? TargetUserId = null);

public sealed record ServiceErasureResult(
    string Service,
    string Treatment,
    int AffectedCount,
    string? Note = null);

internal static class ErasureTreatment
{
    internal const string Anonymised    = "anonymised";
    internal const string Blocked       = "blocked";
    internal const string NotApplicable = "notApplicable";
    internal const string Failed        = "failed";
}
