namespace FPS.Notification.Application;

/// <summary>
/// LOC001 (#744) — service-wide default locale for compose-time text (in-app notifications,
/// transactional email, the verification email). Bound from configuration key
/// <c>Notification:DefaultLocale</c> (env override: <c>Notification__DefaultLocale</c>).
///
/// This is a stand-in for real per-tenant/per-recipient locale resolution, which is a documented
/// follow-up for a later slice — today every notification composed by a given service instance uses
/// this same default, regardless of the recipient.
/// </summary>
public sealed class NotificationLocaleOptions
{
    public const string SectionName = "Notification";

    public string DefaultLocale { get; init; } = NotificationMessages.DefaultLocale;
}
