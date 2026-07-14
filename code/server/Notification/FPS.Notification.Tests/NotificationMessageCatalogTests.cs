using FPS.Notification.Application;

namespace FPS.Notification.Tests;

// LOC001 (#744) — direct unit coverage for the EN/CS message catalog: locale normalization, the
// English fallback for missing keys/unrecognised locales, and locale-correct date formatting.
public sealed class NotificationMessageCatalogTests
{
    [Theory]
    [InlineData("cs")]
    [InlineData("cs-CZ")]
    [InlineData("CS-cz")]
    public void NormalizeLocale_CzechVariants_NormalizeToCs(string input)
    {
        Assert.Equal("cs", NotificationMessages.NormalizeLocale(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr-FR")]
    [InlineData("de")]
    [InlineData("not-a-locale")]
    public void NormalizeLocale_UnsupportedOrMissing_FallsBackToEnglish(string? input)
    {
        Assert.Equal("en", NotificationMessages.NormalizeLocale(input));
    }

    [Fact]
    public void Resolve_UnknownLocale_FallsBackToEnglishText()
    {
        var value = NotificationMessages.Resolve("fr-FR", "booking.slotAllocated.subject");

        Assert.Equal("Your parking spot is confirmed", value);
    }

    [Fact]
    public void Resolve_KnownCzechKey_ReturnsCzechText()
    {
        var value = NotificationMessages.Resolve("cs-CZ", "booking.slotAllocated.subject");

        Assert.Equal("Vaše parkovací místo je potvrzeno", value);
    }

    [Fact]
    public void Resolve_MissingKeyInBothCatalogs_ReturnsKeyItself()
    {
        var value = NotificationMessages.Resolve("cs-CZ", "no.such.key");

        Assert.Equal("no.such.key", value);
    }

    [Fact]
    public void TryResolve_MissingKey_ReturnsFalse()
    {
        var found = NotificationMessages.TryResolve("en", "no.such.key", out var value);

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void Resolve_WithArgs_FormatsPositionalPlaceholders()
    {
        var value = NotificationMessages.Resolve("en", "booking.requestSubmitted.message", " for 12 May 2026");

        Assert.Equal("Your parking request for 12 May 2026 has been submitted and is waiting for draw allocation.", value);
    }

    [Fact]
    public void FormatDate_English_UsesDayAbbreviatedMonthYearPattern()
    {
        var formatted = NotificationMessages.FormatDate(new DateOnly(2026, 7, 14), "en");

        Assert.Equal("14 Jul 2026", formatted);
    }

    [Fact]
    public void FormatDate_Czech_UsesCultureCorrectDayMonthYearPattern()
    {
        var formatted = NotificationMessages.FormatDate(new DateOnly(2026, 7, 14), "cs-CZ");

        Assert.Equal("14. 7. 2026", formatted);
    }

    [Fact]
    public void FormatDate_UnparsableRawDate_ReturnsInputUnchanged()
    {
        var formatted = NotificationMessages.FormatDate("not-a-date", "cs-CZ");

        Assert.Equal("not-a-date", formatted);
    }

    [Fact]
    public void NeverTranslatesProductNames()
    {
        // House-style guard: "FairSpot" must appear verbatim in Czech copy, never translated.
        Assert.Contains("FairSpot", NotificationMessages.Resolve("cs-CZ", "verification.subject"));
        Assert.Contains("FairSpot", NotificationMessages.Resolve("cs-CZ", "email.footer.default"));
    }

    [Fact]
    public void CzechCopy_NeverUsesTheWordProblem()
    {
        // House-style guard: never the word "problém" in Czech customer-facing copy.
        foreach (var key in new[]
        {
            "rejection.ProfileUnavailable", "booking.requestRejected.noReason.message",
            "booking.penaltyApplied.message", "booking.noShowRecorded.message",
        })
        {
            var text = NotificationMessages.Resolve("cs-CZ", key, "X", "Y", "Z");
            Assert.DoesNotContain("problém", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
