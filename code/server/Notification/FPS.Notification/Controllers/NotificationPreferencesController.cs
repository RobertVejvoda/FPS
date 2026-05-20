using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

[ApiController]
[Route("notifications/preferences")]
[Authorize]
public sealed class NotificationPreferencesController(
    INotificationPreferencesRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var prefs = await repository.GetOrDefaultAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        return Ok(ToDto(prefs));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var prefs = await repository.GetOrDefaultAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        prefs.Update(request.RemindersEnabled, request.InformationalEnabled, request.PreferredReminderTiming);
        await repository.SaveAsync(prefs, cancellationToken);
        return Ok(ToDto(prefs));
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences p) => new(
        p.RemindersEnabled,
        p.InformationalEnabled,
        p.PreferredReminderTiming);
}

public sealed record UpdateNotificationPreferencesRequest(
    bool RemindersEnabled,
    bool InformationalEnabled,
    string? PreferredReminderTiming);

public sealed record NotificationPreferencesDto(
    bool RemindersEnabled,
    bool InformationalEnabled,
    string? PreferredReminderTiming);
