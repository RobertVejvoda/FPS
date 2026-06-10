using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Authorize(Roles = "admin,hr_manager")]
public sealed class ProfileHrController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Returns display names for a batch of subject hashes. HR/admin only.
    /// Returns an empty string for any hash with no stored display name.
    /// </summary>
    [HttpPost("/profile/hr/display-names")]
    [ProducesResponseType(typeof(Dictionary<string, string?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDisplayNames(
        [FromBody] IReadOnlyList<string> userIds,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var userId in (userIds ?? []).Distinct())
        {
            if (string.IsNullOrWhiteSpace(userId)) continue;
            var profile = await repository.GetAsync(currentUser.TenantId, userId, ct);
            result[userId] = profile?.DisplayName;
        }

        return Ok(result);
    }
}
