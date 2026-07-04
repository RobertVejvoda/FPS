using FPS.Notification.Domain;

namespace FPS.Notification.Application;

/// <summary>
/// NOTIF #727 — turns a stored <see cref="NotificationRecord"/> into customer-ready email content:
/// an event-specific subject, an HTML body, and a plain-text fallback. Composition is a product/
/// content concern kept separate from transport: the sender receives already-composed content and
/// never builds subject/body itself. All dynamic values are HTML-encoded here.
/// </summary>
public interface IEmailNotificationComposer
{
    ComposedEmail Compose(NotificationRecord record);
}

/// <summary>Provider-agnostic composed email content. HtmlBody is already escaped and layout-wrapped.</summary>
public sealed record ComposedEmail(string Subject, string HtmlBody, string TextBody);
