using System.Net;
using System.Text;
using FPS.Notification.Application;
using FPS.Notification.Domain;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #727 — composes event-specific, business-safe email content from a stored notification
/// record. One reusable inline-CSS layout wraps a per-type subject/heading/status plus the record's
/// message and available details (date, time slot, location) and a next-action hint. Every dynamic
/// value is HTML-encoded; a plain-text twin is produced alongside the HTML. No allocation internals,
/// GUIDs, seeds, weights, stack traces, secrets, or internal URLs are ever rendered.
/// </summary>
public sealed class EmailNotificationComposer : IEmailNotificationComposer
{
    private const string BrandName = "FairSpot";
    private const string HrSuffix = ".hr";

    public ComposedEmail Compose(NotificationRecord record, string locale = NotificationMessages.DefaultLocale)
    {
        // Prefer a variant-specific template key (keyed on safe outcome differentiators), then the base
        // NotificationType, then a safe generic fallback — same priority as before LOC001, now resolved
        // through the message catalog instead of a locally hard-coded English dictionary.
        var refinedKey = ResolveVariantKey(record);
        var subject = ResolveField(refinedKey, record.NotificationType, "subject", locale);
        var heading = ResolveField(refinedKey, record.NotificationType, "heading", locale);
        var status = ResolveField(refinedKey, record.NotificationType, "status", locale);

        var details = BuildDetails(record, locale);
        var nextAction = ResolveNextAction(record.NextAction, locale);
        var footer = ResolveFooter(record.NotificationType, locale);
        var nextStepLabel = NotificationMessages.Resolve(locale, "email.nextStepLabel");

        return new ComposedEmail(
            subject,
            RenderHtml(heading, status, record.MessageText, details, nextAction, nextStepLabel, footer),
            RenderText(heading, status, record.MessageText, details, nextAction, nextStepLabel, footer));
    }

    private static string ResolveField(string? refinedKey, string notificationType, string suffix, string locale)
    {
        if (refinedKey is not null && NotificationMessages.TryResolve(locale, $"{refinedKey}.{suffix}", out var refined))
            return refined;
        if (NotificationMessages.TryResolve(locale, $"{notificationType}.{suffix}", out var baseValue))
            return baseValue;
        return NotificationMessages.Resolve(locale, $"email.fallback.{suffix}");
    }

    // Maps shared NotificationTypes to a variant key using only business-safe differentiators. Returns
    // null when the base template applies. Unrecognised values fall through to the base template.
    private static string? ResolveVariantKey(NotificationRecord record) => record.NotificationType switch
    {
        "booking.slotAllocated" when IsReallocation(record.AllocationSource) => "booking.slotAllocated.reallocation",
        "booking.requestCancelled" when IsAllocatedStatus(record.PreviousStatus) => "booking.requestCancelled.postAllocation",
        "booking.penaltyApplied" when record.ReasonCode is "LateCancellation" or "NoShow" => $"booking.penaltyApplied.{record.ReasonCode}",
        _ => null,
    };

    private static bool IsReallocation(string? source) =>
        string.Equals(source, "reallocation", StringComparison.OrdinalIgnoreCase);

    // The cancelled reservation was already allocated (vs cancelled before allocation). Booking now
    // carries the pre-cancellation status on the cancel event (#727), so this variant is delivered
    // end-to-end; the check tolerates future allocated-like statuses too.
    private static bool IsAllocatedStatus(string? previousStatus) =>
        string.Equals(previousStatus, "Allocated", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(previousStatus, "Reallocated", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(string Label, string Value)> BuildDetails(NotificationRecord record, string locale)
    {
        var rows = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(record.RelatedDate))
            rows.Add((NotificationMessages.Resolve(locale, "email.label.date"), NotificationMessages.FormatDate(record.RelatedDate!, locale)));
        if (!string.IsNullOrWhiteSpace(record.RelatedTimeSlot))
            rows.Add((NotificationMessages.Resolve(locale, "email.label.timeSlot"), record.RelatedTimeSlot!));
        if (!string.IsNullOrWhiteSpace(record.LocationId))
            rows.Add((NotificationMessages.Resolve(locale, "email.label.location"), record.LocationId!));
        return rows;
    }

    private static string? ResolveNextAction(string? nextAction, string locale) => nextAction switch
    {
        "confirmUsage" => NotificationMessages.Resolve(locale, "email.nextAction.confirmUsage"),
        "cancel" => NotificationMessages.Resolve(locale, "email.nextAction.cancel"),
        _ => null,
    };

    private static string ResolveFooter(string notificationType, string locale)
    {
        if (notificationType.EndsWith(HrSuffix, StringComparison.Ordinal))
            return NotificationMessages.Resolve(locale, "email.footer.hr");
        if (notificationType == "tenant-request.received")
            return NotificationMessages.Resolve(locale, "email.footer.salesInbox");
        return NotificationMessages.Resolve(locale, "email.footer.default");
    }

    // ── HTML rendering (inline CSS, table layout, email-client safe) ────────────────
    private static string RenderHtml(
        string heading, string status, string message, IReadOnlyList<(string Label, string Value)> details,
        string? nextAction, string nextStepLabel, string footer)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"margin:0;padding:0;background-color:#f4f5f7;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f4f5f7;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">");
        sb.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;\">");

        // Brand header
        sb.Append("<tr><td style=\"background-color:#1f2937;padding:16px 24px;\">");
        sb.Append($"<span style=\"color:#ffffff;font-size:18px;font-weight:bold;letter-spacing:0.5px;\">{Encode(BrandName)}</span>");
        sb.Append("</td></tr>");

        // Title + status
        sb.Append("<tr><td style=\"padding:24px 24px 8px 24px;\">");
        sb.Append($"<h1 style=\"margin:0;font-size:20px;color:#111827;\">{Encode(heading)}</h1>");
        sb.Append($"<p style=\"margin:8px 0 0 0;font-size:13px;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;\">{Encode(status)}</p>");
        sb.Append("</td></tr>");

        // Message
        sb.Append("<tr><td style=\"padding:8px 24px 0 24px;\">");
        sb.Append($"<p style=\"margin:0;font-size:15px;line-height:1.5;color:#374151;\">{EncodeMultiline(message)}</p>");
        sb.Append("</td></tr>");

        // Detail rows
        if (details.Count > 0)
        {
            sb.Append("<tr><td style=\"padding:16px 24px 0 24px;\">");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f9fafb;border-radius:6px;\">");
            foreach (var (label, value) in details)
            {
                sb.Append("<tr>");
                sb.Append($"<td style=\"padding:8px 12px;font-size:13px;color:#6b7280;width:110px;\">{Encode(label)}</td>");
                sb.Append($"<td style=\"padding:8px 12px;font-size:13px;color:#111827;font-weight:bold;\">{Encode(value)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></td></tr>");
        }

        // Next action
        if (nextAction is not null)
        {
            sb.Append("<tr><td style=\"padding:16px 24px 0 24px;\">");
            sb.Append($"<p style=\"margin:0;font-size:14px;line-height:1.5;color:#1f2937;\"><strong>{Encode(nextStepLabel)}</strong> {Encode(nextAction)}</p>");
            sb.Append("</td></tr>");
        }

        // Footer
        sb.Append("<tr><td style=\"padding:24px;\">");
        sb.Append("<hr style=\"border:none;border-top:1px solid #e5e7eb;margin:0 0 12px 0;\">");
        sb.Append($"<p style=\"margin:0;font-size:12px;line-height:1.5;color:#9ca3af;\">{Encode(footer)}</p>");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></div>");
        return sb.ToString();
    }

    // ── Plain-text rendering ────────────────────────────────────────────────────────
    private static string RenderText(
        string heading, string status, string message, IReadOnlyList<(string Label, string Value)> details,
        string? nextAction, string nextStepLabel, string footer)
    {
        var sb = new StringBuilder();
        sb.Append(BrandName).Append('\n');
        sb.Append(heading).Append(" (").Append(status).Append(")\n\n");
        sb.Append(message.Trim()).Append('\n');
        if (details.Count > 0)
        {
            sb.Append('\n');
            foreach (var (label, value) in details)
                sb.Append(label).Append(": ").Append(value).Append('\n');
        }
        if (nextAction is not null)
            sb.Append('\n').Append(nextStepLabel).Append(' ').Append(nextAction).Append('\n');
        sb.Append("\n—\n").Append(footer).Append('\n');
        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // Encode then turn newlines into <br> so multi-line messages keep their breaks without
    // allowing raw HTML through (encoding happens before the <br> substitution).
    private static string EncodeMultiline(string value) =>
        Encode(value)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
}
