using FPS.Notification.Application;
using FPS.Notification.Domain;
using FPS.Notification.Infrastructure;

namespace FPS.Notification.Tests.Infrastructure;

public sealed class EmailNotificationComposerTests
{
    private readonly EmailNotificationComposer composer = new();

    private static NotificationRecord Record(
        string type,
        string message = "Your parking request has an update.",
        string? date = "2026-05-14",
        string? timeSlot = "09:00-17:00",
        string? location = "GL-HQ",
        string? nextAction = null,
        string? allocationSource = null,
        string? reasonCode = null,
        string? previousStatus = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = "tenant-1",
        RecipientId = "ops@fairspot.net",
        NotificationType = type,
        Channel = NotificationChannel.Email,
        MessageText = message,
        RelatedDate = date,
        RelatedTimeSlot = timeSlot,
        LocationId = location,
        NextAction = nextAction,
        AllocationSource = allocationSource,
        ReasonCode = reasonCode,
        PreviousStatus = previousStatus,
        SourceEventId = "event-1",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void Compose_Reallocation_UsesDistinctTemplate_FromNormalAllocation()
    {
        var normal = composer.Compose(Record("booking.slotAllocated"));
        var realloc = composer.Compose(Record("booking.slotAllocated", allocationSource: "reallocation"));

        Assert.Equal("Your parking spot is confirmed", normal.Subject);
        Assert.Equal("A parking spot was reallocated to you", realloc.Subject);
        Assert.Contains("Parking spot reallocated", realloc.HtmlBody);
        Assert.Contains("Reallocated", realloc.HtmlBody);
    }

    [Fact]
    public void Compose_AllocatedReservationCancelled_UsesDistinctTemplate()
    {
        var beforeAlloc = composer.Compose(Record("booking.requestCancelled"));
        var afterAlloc = composer.Compose(Record("booking.requestCancelled", previousStatus: "Allocated"));

        Assert.Equal("Your parking request was cancelled", beforeAlloc.Subject);
        Assert.Equal("Your allocated parking reservation was cancelled", afterAlloc.Subject);
    }

    [Theory]
    [InlineData("LateCancellation", "A late-cancellation penalty was applied")]
    [InlineData("NoShow", "A no-show penalty was applied")]
    public void Compose_PenaltyVariants_UseReasonSpecificSubjects(string reasonCode, string expectedSubject)
    {
        var email = composer.Compose(Record("booking.penaltyApplied", reasonCode: reasonCode));

        Assert.Equal(expectedSubject, email.Subject);
    }

    [Fact]
    public void Compose_PenaltyWithUnknownReason_FallsBackToBasePenaltyTemplate()
    {
        var email = composer.Compose(Record("booking.penaltyApplied", reasonCode: "Something"));

        Assert.Equal("A parking penalty was applied", email.Subject);
    }

    [Theory]
    [InlineData("booking.requestSubmitted", "Your parking request was submitted")]
    [InlineData("booking.requestRejected", "Your parking request could not be allocated")]
    [InlineData("booking.slotAllocated", "Your parking spot is confirmed")]
    [InlineData("booking.requestCancelled", "Your parking request was cancelled")]
    [InlineData("booking.drawCompleted", "Your parking allocation results")]
    [InlineData("booking.noShowRecorded", "Parking no-show recorded")]
    [InlineData("booking.penaltyApplied", "A parking penalty was applied")]
    [InlineData("booking.manualCorrectionApplied", "Your parking request was updated")]
    [InlineData("booking.requestSubmitted.hr", "New parking request submitted")]
    [InlineData("booking.drawCompleted.hr", "Parking draw completed")]
    [InlineData("tenant-request.received", "New FairSpot pilot request")]
    public void Compose_UsesEventSpecificSubject(string type, string expectedSubject)
    {
        var email = composer.Compose(Record(type));

        Assert.Equal(expectedSubject, email.Subject);
    }

    [Fact]
    public void Compose_UnknownType_FallsBackToSafeGenericSubject()
    {
        var email = composer.Compose(Record("booking.somethingNew"));

        Assert.Equal("FairSpot notification", email.Subject);
    }

    [Fact]
    public void Compose_ProducesBothHtmlAndPlainText()
    {
        var email = composer.Compose(Record("booking.slotAllocated"));

        Assert.Contains("<div", email.HtmlBody, StringComparison.OrdinalIgnoreCase); // wrapped in layout markup
        Assert.Contains("<td", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.False(email.TextBody.Contains('<'), "plain-text body must not contain HTML tags");
        Assert.Contains("FairSpot", email.TextBody);
    }

    [Fact]
    public void Compose_EscapesHtmlInjectionInMessage_AndDoesNotRenderRawTags()
    {
        var record = Record("booking.slotAllocated",
            message: "Allocated <script>alert('x')</script> & <b>bold</b> spot.");

        var email = composer.Compose(record);

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.DoesNotContain("<b>bold</b>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        Assert.Contains("&amp;", email.HtmlBody);
    }

    [Fact]
    public void Compose_EscapesHtmlInjectionInDetailValues()
    {
        var record = Record("booking.slotAllocated", location: "<img src=x onerror=alert(1)>");

        var email = composer.Compose(record);

        Assert.DoesNotContain("<img", email.HtmlBody);
        Assert.Contains("&lt;img", email.HtmlBody);
    }

    [Fact]
    public void Compose_MissingOptionalValues_DegradeGracefully_NoRawNulls()
    {
        var record = Record("booking.requestSubmitted",
            date: null, timeSlot: null, location: null, nextAction: null);

        var email = composer.Compose(record);

        Assert.DoesNotContain("null", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", email.TextBody, StringComparison.OrdinalIgnoreCase);
        // With no details, the detail label row must not be rendered.
        Assert.DoesNotContain(">Date<", email.HtmlBody);
        Assert.DoesNotContain(">Location<", email.HtmlBody);
    }

    [Fact]
    public void Compose_IncludesAvailableDetails_WhenPresent()
    {
        var email = composer.Compose(Record("booking.slotAllocated"));

        Assert.Contains("14 May 2026", email.HtmlBody); // date formatted
        Assert.Contains("09:00-17:00", email.HtmlBody);
        Assert.Contains("GL-HQ", email.HtmlBody);
        Assert.Contains("14 May 2026", email.TextBody);
    }

    [Fact]
    public void Compose_RendersNextAction_WhenPresent_AndOmitsWhenAbsent()
    {
        var withAction = composer.Compose(Record("booking.slotAllocated", nextAction: "confirmUsage"));
        Assert.Contains("Next step", withAction.HtmlBody);
        Assert.Contains("confirm your usage", withAction.HtmlBody);
        Assert.Contains("Next step", withAction.TextBody);

        var withoutAction = composer.Compose(Record("booking.slotAllocated", nextAction: null));
        Assert.DoesNotContain("Next step", withoutAction.HtmlBody);
    }

    [Fact]
    public void Compose_DoesNotExposeAllocationInternalsFromRecordFields()
    {
        // The composer only renders fields it is given; ensure it never surfaces the raw type token
        // or source event id as customer-visible content.
        var record = Record("booking.slotAllocated");

        var email = composer.Compose(record);

        Assert.DoesNotContain("booking.slotAllocated", email.HtmlBody);
        Assert.DoesNotContain("event-1", email.HtmlBody);
        Assert.DoesNotContain(record.Id.ToString(), email.HtmlBody);
    }

    [Fact]
    public void Compose_SalesAlert_IsBusinessReadable()
    {
        var record = Record("tenant-request.received",
            message: "New tenant request: Acme (acme.com). Review and triage in the platform operator queue.",
            date: null, timeSlot: null, location: null);

        var email = composer.Compose(record);

        Assert.Equal("New FairSpot pilot request", email.Subject);
        Assert.Contains("Acme", email.HtmlBody);
        Assert.Contains("sales inbox", email.HtmlBody);
    }
}
