using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Route("profile/hr")]
[Authorize(Roles = "hr_manager,admin")]
public sealed class HrProfileController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    private const int MaxBatchSize = 200;

    /// <summary>
    /// Returns display names for a batch of subject hashes.
    /// Restricted to HR and admin roles. Names are never exposed on employee screens or audit payloads.
    /// </summary>
    [HttpPost("display-names")]
    [ProducesResponseType(typeof(DisplayNamesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDisplayNames(
        [FromBody] DisplayNamesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserIds.Count > MaxBatchSize)
            return BadRequest($"Batch size must not exceed {MaxBatchSize}.");

        var names = new Dictionary<string, string?>(request.UserIds.Count);
        foreach (var userId in request.UserIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var profile = await repository.GetAsync(currentUser.TenantId, userId, cancellationToken);
            names[userId] = profile?.DisplayName;
        }

        return Ok(new DisplayNamesResponse(names));
    }
}

public sealed record DisplayNamesRequest(IReadOnlyList<string> UserIds);
public sealed record DisplayNamesResponse(IReadOnlyDictionary<string, string?> Names);
