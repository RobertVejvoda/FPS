using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FPS.Notification.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public sealed class NotificationController(
    INotificationRepository repository,
    INotificationBroadcaster broadcaster,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? type = null,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 200);

        var records = await repository.GetByRecipientAsync(
            currentUser.TenantId, currentUser.UserId, unreadOnly, type, pageSize, cancellationToken);

        var items = records.Select(ToDto).ToList();
        return Ok(new NotificationListResponse(items, items.Count, items.Count >= pageSize));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        int count = await repository.GetUnreadCountAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        bool found = await repository.MarkReadAsync(notificationId, currentUser.TenantId, currentUser.UserId, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("stream")]
    public async Task StreamAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId) || string.IsNullOrEmpty(currentUser.TenantId))
        {
            Response.StatusCode = 401;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var record in broadcaster.SubscribeAsync(currentUser.TenantId, currentUser.UserId, cancellationToken))
        {
            var json = JsonSerializer.Serialize(ToDto(record));
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static NotificationDto ToDto(NotificationRecord r) => new(
        r.Id,
        r.NotificationType,
        r.MessageText,
        r.RelatedRequestId,
        r.RelatedDate,
        r.RelatedTimeSlot,
        r.LocationId,
        r.NextAction,
        r.IsRead,
        r.CreatedAt);
}

public sealed record NotificationDto(
    Guid Id,
    string NotificationType,
    string MessageText,
    string? RelatedRequestId,
    string? RelatedDate,
    string? RelatedTimeSlot,
    string? LocationId,
    string? NextAction,
    bool IsRead,
    DateTime CreatedAt);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationDto> Items,
    int TotalReturned,
    bool HasMore);

public sealed record UnreadCountResponse(int Count);
