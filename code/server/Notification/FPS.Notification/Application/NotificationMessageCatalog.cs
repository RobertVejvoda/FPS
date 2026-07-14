using System.Globalization;

namespace FPS.Notification.Application;

/// <summary>
/// LOC001 (#744) — small, static English/Czech message catalog for every piece of compose-time text
/// this service produces: in-app notification text (<see cref="BookingEventNotificationHandler"/>),
/// transactional email copy (<c>EmailNotificationComposer</c>), and the verification email
/// (<c>VerificationEmailContent</c>). Localization happens once, at compose time — <c>NotificationRecord
/// .MessageText</c> is persisted already-rendered in the recipient's language, so there is no later
/// re-render-per-viewer path for in-app notifications.
///
/// "FairSpot" and other product names are never looked up here — callers keep them as literal strings
/// in every locale.
/// </summary>
public static class NotificationMessages
{
    public const string DefaultLocale = "en";
    private const string CzechLocale = "cs";

    // ── English (default / fallback for missing keys and unrecognised locales) ─────────────────────
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Rejection reasons — keyed by BookingRejectionCode enum value (employee-safe text only).
        ["rejection.PastDate"] = "The requested date has already passed.",
        ["rejection.CutOffPassed"] = "The submission deadline for this time slot has passed.",
        ["rejection.DailyCapExceeded"] = "The maximum number of requests for this day has been reached.",
        ["rejection.DuplicateRequest"] = "You already have an active request for this time slot.",
        ["rejection.VehicleConstraintUnmatched"] = "Your vehicle does not meet the requirements for this parking area.",
        ["rejection.NoCapacityAvailable"] = "There are no available spots for this time slot.",
        ["rejection.RequestorIneligible"] = "Your account is not currently eligible to request parking.",
        ["rejection.SameDayBookingDisabled"] = "Same-day parking requests are not enabled.",
        ["rejection.NoCapacityForSameDay"] = "No spots are available for same-day allocation.",
        ["rejection.ProfileUnavailable"] = "Your profile information could not be verified. Please check your account.",
        ["rejection.DrawNotSelected"] = "Your request was not selected in the draw for this time slot.",

        // In-app / email body message templates (NOTIF002). {0}/{1}/{2} are positional args.
        ["booking.requestSubmitted.message"] = "Your parking request{0} has been submitted and is waiting for draw allocation.",
        ["booking.requestSubmitted.hr.message"] = "A new parking request{0} has been submitted and is awaiting allocation.",
        ["booking.requestRejected.reason.message"] = "Your parking request{0} could not be processed. {1}",
        ["booking.requestRejected.noReason.message"] = "Your parking request{0} could not be processed.",
        ["booking.slotAllocated.message"] = "A parking spot has been allocated to your request{0}.",
        ["booking.slotAllocated.reallocation.message"] = "A parking spot has been reallocated to your request{0} after a cancellation freed a slot.",
        ["booking.requestCancelled.message"] = "Your parking request{0} has been cancelled.",
        // The requestor's own request was cancelled by HR/admin.
        ["booking.requestCancelled.byHr.reason.message"] = "Your parking request{0} was cancelled by HR. Reason: {1}",
        ["booking.requestCancelled.byHr.noReason.message"] = "Your parking request{0} was cancelled by HR.",
        // HR-audience fan-out: an employee cancelled their own request.
        ["booking.requestCancelled.hr.reason.message"] = "An employee has cancelled their parking request{0}. Reason: {1}",
        ["booking.requestCancelled.hr.noReason.message"] = "An employee has cancelled their parking request{0}.",
        ["booking.drawCompleted.withCounts.message"] = "Parking allocation{0} is complete: {1} of {2} requests allocated.",
        ["booking.drawCompleted.hr.withCounts.message"] = "The parking draw{0} has completed: {1} of {2} requests allocated.",
        ["booking.drawCompleted.noCounts.message"] = "Parking allocation{0} is complete.",
        ["booking.drawCompleted.hr.noCounts.message"] = "The parking draw{0} has completed.",
        ["booking.noShowRecorded.message"] = "Your parking spot{0} was recorded as a no-show. This may affect your future allocation priority.",
        ["booking.penaltyApplied.message"] = "A penalty was applied to your account due to {0}. This may affect your future allocation priority.",
        ["penalty.label.NoShow"] = "no-show",
        ["penalty.label.LateCancellation"] = "late cancellation",
        ["penalty.label.generic"] = "a booking violation",
        ["booking.usageConfirmed.message"] = "Your parking usage{0} has been confirmed.",
        ["booking.requestExpired.message"] = "Your parking request{0} has expired and is no longer active.",
        ["booking.manualCorrectionApplied.reason.message"] = "Your parking request was updated by an authorized administrator. Reason: {0}",
        ["booking.manualCorrectionApplied.noReason.message"] = "Your parking request was updated by an authorized administrator.",
        ["booking.unknown.message"] = "A booking event occurred: {0}.",

        // Date/location/time-slot context clause appended to several messages above.
        ["context.suffix"] = " for {0}{1}{2}",
        ["context.location"] = " at {0}",
        ["context.slot"] = " ({0})",

        // Email subject/heading/status per NotificationType (and business-safe variant key).
        ["booking.requestSubmitted.subject"] = "Your parking request was submitted",
        ["booking.requestSubmitted.heading"] = "Parking request submitted",
        ["booking.requestSubmitted.status"] = "Submitted",
        ["booking.requestSubmitted.hr.subject"] = "New parking request submitted",
        ["booking.requestSubmitted.hr.heading"] = "New parking request",
        ["booking.requestSubmitted.hr.status"] = "Submitted",
        ["booking.requestRejected.subject"] = "Your parking request could not be allocated",
        ["booking.requestRejected.heading"] = "Parking request not allocated",
        ["booking.requestRejected.status"] = "Not allocated",
        ["booking.slotAllocated.subject"] = "Your parking spot is confirmed",
        ["booking.slotAllocated.heading"] = "Parking spot allocated",
        ["booking.slotAllocated.status"] = "Allocated",
        ["booking.slotAllocated.reallocation.subject"] = "A parking spot was reallocated to you",
        ["booking.slotAllocated.reallocation.heading"] = "Parking spot reallocated",
        ["booking.slotAllocated.reallocation.status"] = "Reallocated",
        ["booking.requestCancelled.subject"] = "Your parking request was cancelled",
        ["booking.requestCancelled.heading"] = "Parking request cancelled",
        ["booking.requestCancelled.status"] = "Cancelled",
        ["booking.requestCancelled.hr.subject"] = "A parking request was cancelled",
        ["booking.requestCancelled.hr.heading"] = "Parking request cancelled",
        ["booking.requestCancelled.hr.status"] = "Cancelled",
        ["booking.requestCancelled.postAllocation.subject"] = "Your allocated parking reservation was cancelled",
        ["booking.requestCancelled.postAllocation.heading"] = "Parking reservation cancelled",
        ["booking.requestCancelled.postAllocation.status"] = "Cancelled",
        ["booking.drawCompleted.subject"] = "Your parking allocation results",
        ["booking.drawCompleted.heading"] = "Parking allocation complete",
        ["booking.drawCompleted.status"] = "Draw complete",
        ["booking.drawCompleted.hr.subject"] = "Parking draw completed",
        ["booking.drawCompleted.hr.heading"] = "Parking draw completed",
        ["booking.drawCompleted.hr.status"] = "Draw complete",
        ["booking.noShowRecorded.subject"] = "Parking no-show recorded",
        ["booking.noShowRecorded.heading"] = "No-show recorded",
        ["booking.noShowRecorded.status"] = "No-show",
        ["booking.penaltyApplied.subject"] = "A parking penalty was applied",
        ["booking.penaltyApplied.heading"] = "Parking penalty applied",
        ["booking.penaltyApplied.status"] = "Penalty applied",
        ["booking.penaltyApplied.LateCancellation.subject"] = "A late-cancellation penalty was applied",
        ["booking.penaltyApplied.LateCancellation.heading"] = "Late-cancellation penalty",
        ["booking.penaltyApplied.LateCancellation.status"] = "Penalty applied",
        ["booking.penaltyApplied.NoShow.subject"] = "A no-show penalty was applied",
        ["booking.penaltyApplied.NoShow.heading"] = "No-show penalty",
        ["booking.penaltyApplied.NoShow.status"] = "Penalty applied",
        ["booking.usageConfirmed.subject"] = "Parking usage confirmed",
        ["booking.usageConfirmed.heading"] = "Parking usage confirmed",
        ["booking.usageConfirmed.status"] = "Confirmed",
        ["booking.requestExpired.subject"] = "Your parking request expired",
        ["booking.requestExpired.heading"] = "Parking request expired",
        ["booking.requestExpired.status"] = "Expired",
        ["booking.manualCorrectionApplied.subject"] = "Your parking request was updated",
        ["booking.manualCorrectionApplied.heading"] = "Parking request updated",
        ["booking.manualCorrectionApplied.status"] = "Updated",
        ["tenant-request.received.subject"] = "New FairSpot pilot request",
        ["tenant-request.received.heading"] = "New pilot request",
        ["tenant-request.received.status"] = "New lead",

        ["email.fallback.subject"] = "FairSpot notification",
        ["email.fallback.heading"] = "FairSpot notification",
        ["email.fallback.status"] = "Update",
        ["email.label.date"] = "Date",
        ["email.label.timeSlot"] = "Time slot",
        ["email.label.location"] = "Location",
        ["email.nextAction.confirmUsage"] = "Please confirm your usage in FairSpot once you have parked.",
        ["email.nextAction.cancel"] = "You can cancel this request in FairSpot if your plans change.",
        ["email.footer.hr"] = "You received this as an HR or facilities contact for your organisation's FairSpot workspace.",
        ["email.footer.salesInbox"] = "You received this because it was sent to the FairSpot sales inbox.",
        ["email.footer.default"] = "You received this because it affects your FairSpot parking request.",
        ["email.nextStepLabel"] = "Next step:",

        // AUTH008B (#734) verification email.
        ["verification.subject"] = "Verify your FairSpot email address",
        ["verification.heading"] = "Confirm your email address",
        ["verification.body"] = "Please confirm this email address belongs to you so FairSpot can send you parking notifications. This link expires shortly and can be used once.",
        ["verification.buttonLabel"] = "Verify my email",
        ["verification.fallbackLinkPrefix"] = "If the button does not work, copy this link into your browser:",
        ["verification.footer"] = "If you did not request this, you can safely ignore this email — no changes will be made.",
    };

    // ── Czech (formal "Vy/Váš" address; never translates "FairSpot"/"Green Logistics"; never uses
    // "problém") ──────────────────────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, string> Czech = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rejection.PastDate"] = "Požadované datum již uplynulo.",
        ["rejection.CutOffPassed"] = "Uzávěrka pro tento časový úsek již vypršela.",
        ["rejection.DailyCapExceeded"] = "Byl dosažen maximální počet žádostí pro tento den.",
        ["rejection.DuplicateRequest"] = "Pro tento časový úsek již máte aktivní žádost.",
        ["rejection.VehicleConstraintUnmatched"] = "Vaše vozidlo nesplňuje požadavky pro tuto parkovací zónu.",
        ["rejection.NoCapacityAvailable"] = "Pro tento časový úsek nejsou k dispozici žádná volná parkovací místa.",
        ["rejection.RequestorIneligible"] = "Váš účet momentálně není oprávněn žádat o parkovací místo.",
        ["rejection.SameDayBookingDisabled"] = "Žádosti o parkovací místo v tentýž den nejsou povoleny.",
        ["rejection.NoCapacityForSameDay"] = "Pro přidělení v tentýž den nejsou k dispozici žádná volná parkovací místa.",
        ["rejection.ProfileUnavailable"] = "Údaje ve Vašem profilu se nepodařilo ověřit. Zkontrolujte prosím svůj účet.",
        ["rejection.DrawNotSelected"] = "Vaše žádost nebyla vybrána v losování pro tento časový úsek.",

        ["booking.requestSubmitted.message"] = "Vaše žádost o parkovací místo{0} byla podána a čeká na losování.",
        ["booking.requestSubmitted.hr.message"] = "Byla podána nová žádost o parkovací místo{0} a čeká na přidělení.",
        ["booking.requestRejected.reason.message"] = "Vaši žádost o parkovací místo{0} se nepodařilo zpracovat. {1}",
        ["booking.requestRejected.noReason.message"] = "Vaši žádost o parkovací místo{0} se nepodařilo zpracovat.",
        ["booking.slotAllocated.message"] = "Parkovací místo bylo přiděleno k Vaší žádosti{0}.",
        ["booking.slotAllocated.reallocation.message"] = "Parkovací místo bylo znovu přiděleno k Vaší žádosti{0} poté, co se zrušením uvolnilo místo.",
        ["booking.requestCancelled.message"] = "Vaše žádost o parkovací místo{0} byla zrušena.",
        ["booking.requestCancelled.byHr.reason.message"] = "Vaše žádost o parkovací místo{0} byla zrušena personálním oddělením. Důvod: {1}",
        ["booking.requestCancelled.byHr.noReason.message"] = "Vaše žádost o parkovací místo{0} byla zrušena personálním oddělením.",
        ["booking.requestCancelled.hr.reason.message"] = "Zaměstnanec zrušil svou žádost o parkovací místo{0}. Důvod: {1}",
        ["booking.requestCancelled.hr.noReason.message"] = "Zaměstnanec zrušil svou žádost o parkovací místo{0}.",
        ["booking.drawCompleted.withCounts.message"] = "Přidělování parkovacích míst{0} je dokončeno: přiděleno {1} z {2} žádostí.",
        ["booking.drawCompleted.hr.withCounts.message"] = "Losování parkovacích míst{0} bylo dokončeno: přiděleno {1} z {2} žádostí.",
        ["booking.drawCompleted.noCounts.message"] = "Přidělování parkovacích míst{0} je dokončeno.",
        ["booking.drawCompleted.hr.noCounts.message"] = "Losování parkovacích míst{0} bylo dokončeno.",
        ["booking.noShowRecorded.message"] = "Vaše parkovací místo{0} bylo zaznamenáno jako nevyužití rezervace. Může to ovlivnit prioritu Vašich budoucích přidělení.",
        ["booking.penaltyApplied.message"] = "Na Váš účet byla uplatněna sankce z důvodu: {0}. Může to ovlivnit prioritu Vašich budoucích přidělení.",
        ["penalty.label.NoShow"] = "nevyužití rezervace",
        ["penalty.label.LateCancellation"] = "pozdní zrušení",
        ["penalty.label.generic"] = "porušení pravidel rezervace",
        ["booking.usageConfirmed.message"] = "Využití Vašeho parkovacího místa{0} bylo potvrzeno.",
        ["booking.requestExpired.message"] = "Platnost Vaší žádosti o parkovací místo{0} vypršela a již není aktivní.",
        ["booking.manualCorrectionApplied.reason.message"] = "Vaši žádost o parkovací místo upravil oprávněný administrátor. Důvod: {0}",
        ["booking.manualCorrectionApplied.noReason.message"] = "Vaši žádost o parkovací místo upravil oprávněný administrátor.",
        ["booking.unknown.message"] = "Nastala událost rezervace: {0}.",

        ["context.suffix"] = " na {0}{1}{2}",
        ["context.location"] = " v {0}",
        ["context.slot"] = " ({0})",

        ["booking.requestSubmitted.subject"] = "Vaše žádost o parkovací místo byla podána",
        ["booking.requestSubmitted.heading"] = "Žádost o parkovací místo podána",
        ["booking.requestSubmitted.status"] = "Podáno",
        ["booking.requestSubmitted.hr.subject"] = "Byla podána nová žádost o parkovací místo",
        ["booking.requestSubmitted.hr.heading"] = "Nová žádost o parkovací místo",
        ["booking.requestSubmitted.hr.status"] = "Podáno",
        ["booking.requestRejected.subject"] = "Vaši žádost o parkovací místo se nepodařilo přidělit",
        ["booking.requestRejected.heading"] = "Žádost o parkovací místo nebyla přidělena",
        ["booking.requestRejected.status"] = "Nepřiděleno",
        ["booking.slotAllocated.subject"] = "Vaše parkovací místo je potvrzeno",
        ["booking.slotAllocated.heading"] = "Parkovací místo přiděleno",
        ["booking.slotAllocated.status"] = "Přiděleno",
        ["booking.slotAllocated.reallocation.subject"] = "Bylo Vám znovu přiděleno parkovací místo",
        ["booking.slotAllocated.reallocation.heading"] = "Parkovací místo znovu přiděleno",
        ["booking.slotAllocated.reallocation.status"] = "Znovu přiděleno",
        ["booking.requestCancelled.subject"] = "Vaše žádost o parkovací místo byla zrušena",
        ["booking.requestCancelled.heading"] = "Žádost o parkovací místo zrušena",
        ["booking.requestCancelled.status"] = "Zrušeno",
        ["booking.requestCancelled.hr.subject"] = "Žádost o parkovací místo byla zrušena",
        ["booking.requestCancelled.hr.heading"] = "Žádost o parkovací místo zrušena",
        ["booking.requestCancelled.hr.status"] = "Zrušeno",
        ["booking.requestCancelled.postAllocation.subject"] = "Vaše přidělená rezervace parkování byla zrušena",
        ["booking.requestCancelled.postAllocation.heading"] = "Rezervace parkování zrušena",
        ["booking.requestCancelled.postAllocation.status"] = "Zrušeno",
        ["booking.drawCompleted.subject"] = "Výsledky přidělení parkovacích míst",
        ["booking.drawCompleted.heading"] = "Přidělení parkovacích míst dokončeno",
        ["booking.drawCompleted.status"] = "Losování dokončeno",
        ["booking.drawCompleted.hr.subject"] = "Losování parkovacích míst bylo dokončeno",
        ["booking.drawCompleted.hr.heading"] = "Losování parkovacích míst dokončeno",
        ["booking.drawCompleted.hr.status"] = "Losování dokončeno",
        ["booking.noShowRecorded.subject"] = "Zaznamenáno nevyužití rezervace parkovacího místa",
        ["booking.noShowRecorded.heading"] = "Nevyužití rezervace zaznamenáno",
        ["booking.noShowRecorded.status"] = "Nevyužití rezervace",
        ["booking.penaltyApplied.subject"] = "Byla uplatněna sankce za parkování",
        ["booking.penaltyApplied.heading"] = "Sankce za parkování uplatněna",
        ["booking.penaltyApplied.status"] = "Sankce uplatněna",
        ["booking.penaltyApplied.LateCancellation.subject"] = "Byla uplatněna sankce za pozdní zrušení",
        ["booking.penaltyApplied.LateCancellation.heading"] = "Sankce za pozdní zrušení",
        ["booking.penaltyApplied.LateCancellation.status"] = "Sankce uplatněna",
        ["booking.penaltyApplied.NoShow.subject"] = "Byla uplatněna sankce za nevyužití rezervace",
        ["booking.penaltyApplied.NoShow.heading"] = "Sankce za nevyužití rezervace",
        ["booking.penaltyApplied.NoShow.status"] = "Sankce uplatněna",
        ["booking.usageConfirmed.subject"] = "Využití parkovacího místa potvrzeno",
        ["booking.usageConfirmed.heading"] = "Využití parkovacího místa potvrzeno",
        ["booking.usageConfirmed.status"] = "Potvrzeno",
        ["booking.requestExpired.subject"] = "Platnost Vaší žádosti o parkovací místo vypršela",
        ["booking.requestExpired.heading"] = "Žádost o parkovací místo vypršela",
        ["booking.requestExpired.status"] = "Vypršelo",
        ["booking.manualCorrectionApplied.subject"] = "Vaše žádost o parkovací místo byla upravena",
        ["booking.manualCorrectionApplied.heading"] = "Žádost o parkovací místo upravena",
        ["booking.manualCorrectionApplied.status"] = "Upraveno",
        ["tenant-request.received.subject"] = "Nová žádost o pilotní provoz FairSpot",
        ["tenant-request.received.heading"] = "Nová žádost o pilotní provoz",
        ["tenant-request.received.status"] = "Nový kontakt",

        ["email.fallback.subject"] = "Oznámení FairSpot",
        ["email.fallback.heading"] = "Oznámení FairSpot",
        ["email.fallback.status"] = "Aktualizace",
        ["email.label.date"] = "Datum",
        ["email.label.timeSlot"] = "Časový úsek",
        ["email.label.location"] = "Lokalita",
        ["email.nextAction.confirmUsage"] = "Jakmile zaparkujete, potvrďte prosím využití místa ve FairSpot.",
        ["email.nextAction.cancel"] = "Pokud se Vaše plány změní, můžete tuto žádost ve FairSpot zrušit.",
        ["email.footer.hr"] = "Tento e-mail jste obdrželi jako kontaktní osoba pro personální oddělení nebo správu budov ve Vašem pracovním prostoru FairSpot.",
        ["email.footer.salesInbox"] = "Tento e-mail jste obdrželi, protože byl odeslán do prodejní schránky FairSpot.",
        ["email.footer.default"] = "Tento e-mail jste obdrželi, protože se týká Vaší žádosti o parkovací místo ve FairSpot.",
        ["email.nextStepLabel"] = "Další krok:",

        ["verification.subject"] = "Ověřte svou e-mailovou adresu FairSpot",
        ["verification.heading"] = "Potvrďte svou e-mailovou adresu",
        ["verification.body"] = "Potvrďte prosím, že tato e-mailová adresa patří Vám, aby Vám mohl FairSpot zasílat oznámení o parkování. Tento odkaz brzy vyprší a lze jej použít pouze jednou.",
        ["verification.buttonLabel"] = "Ověřit e-mail",
        ["verification.fallbackLinkPrefix"] = "Pokud tlačítko nefunguje, zkopírujte tento odkaz do prohlížeče:",
        ["verification.footer"] = "Pokud jste o to nežádali, tento e-mail můžete bez obav ignorovat — nic se nezmění.",
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultLocale] = English,
            [CzechLocale] = Czech,
        };

    // Custom date patterns per locale. "en" mirrors the historical "d MMM yyyy" rendering (e.g.
    // "12 May 2026"); "cs" renders the culture-correct Czech day.month.year pattern (e.g. "14. 7. 2026").
    private static readonly IReadOnlyDictionary<string, string> DateFormats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultLocale] = "d MMM yyyy",
        [CzechLocale] = "d. M. yyyy",
    };

    private static readonly IReadOnlyDictionary<string, CultureInfo> Cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultLocale] = CultureInfo.GetCultureInfo("en-US"),
        [CzechLocale] = CultureInfo.GetCultureInfo("cs-CZ"),
    };

    /// <summary>Resolves and formats a message. Falls back to English, then to the raw key, so a
    /// translation gap can never surface as blank customer-facing text.</summary>
    public static string Resolve(string? locale, string key, params object?[] args) =>
        TryResolve(locale, key, out var value, args) ? value : key;

    /// <summary>Like <see cref="Resolve"/> but reports whether the key exists (in the requested locale
    /// or the English fallback) instead of degrading to the raw key. Used where "no known text for this
    /// code" is itself a meaningful outcome (e.g. an unrecognised rejection reason code).</summary>
    public static bool TryResolve(string? locale, string key, out string value, params object?[] args)
    {
        var template = FindTemplate(locale, key);
        if (template is null)
        {
            value = string.Empty;
            return false;
        }

        value = args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
        return true;
    }

    /// <summary>Normalizes a BCP-47-ish locale string ("cs-CZ", "cs", "en-US"...) to the bare language
    /// tag this catalog indexes by. Null/empty/unsupported locales fall back to English.</summary>
    public static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return DefaultLocale;
        var lang = locale.Split('-')[0].Trim().ToLowerInvariant();
        return Catalogs.ContainsKey(lang) ? lang : DefaultLocale;
    }

    /// <summary>Formats a user-visible date using the active locale's own <see cref="CultureInfo"/> —
    /// never <see cref="CultureInfo.InvariantCulture"/> or the ambient current culture — so cs-CZ
    /// renders "14. 7. 2026" rather than an English-shaped pattern. Not for machine-readable output.</summary>
    public static string FormatDate(string rawDate, string? locale) =>
        DateOnly.TryParse(rawDate, CultureInfo.InvariantCulture, out var date)
            ? FormatDate(date, locale)
            : rawDate;

    public static string FormatDate(DateOnly date, string? locale)
    {
        var normalized = NormalizeLocale(locale);
        return date.ToString(DateFormats[normalized], Cultures[normalized]);
    }

    private static string? FindTemplate(string? locale, string key)
    {
        var normalized = NormalizeLocale(locale);
        if (!string.Equals(normalized, DefaultLocale, StringComparison.OrdinalIgnoreCase) &&
            Catalogs[normalized].TryGetValue(key, out var localized))
        {
            return localized;
        }

        return Catalogs[DefaultLocale].TryGetValue(key, out var english) ? english : null;
    }
}
