using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Tests.Models;

public sealed class BookingListItemTests
{
    private static BookingListItem RejectedItem(string? reasonCode, string? reason) => new(
        RequestId: Guid.NewGuid(),
        RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow),
        TimeSlotStart: new TimeOnly(9, 0),
        TimeSlotEnd: new TimeOnly(17, 0),
        LocationId: null,
        Status: "Rejected",
        ReasonCode: reasonCode,
        Reason: reason,
        AllocatedSlotId: null,
        NextAction: "none",
        CreatedAt: DateTime.UtcNow,
        LastStatusChangedAt: DateTime.UtcNow);

    [Fact]
    public void RejectedItem_WithReasonCode_ExposesCode()
    {
        var item = RejectedItem("DrawNotSelected", "Not selected in draw");

        Assert.Equal("DrawNotSelected", item.ReasonCode);
        Assert.Equal("Not selected in draw", item.Reason);
    }

    [Fact]
    public void RejectedItem_WithoutReasonCode_ReasonCodeIsNull()
    {
        var item = RejectedItem(null, "Legacy reason text");

        Assert.Null(item.ReasonCode);
        Assert.Equal("Legacy reason text", item.Reason);
    }

    [Fact]
    public void PendingItem_ReasonCodeIsNull()
    {
        var item = new BookingListItem(
            RequestId: Guid.NewGuid(),
            RequestedDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            TimeSlotStart: new TimeOnly(9, 0),
            TimeSlotEnd: new TimeOnly(17, 0),
            LocationId: null,
            Status: "Pending",
            ReasonCode: null,
            Reason: null,
            AllocatedSlotId: null,
            NextAction: "cancel",
            CreatedAt: DateTime.UtcNow,
            LastStatusChangedAt: DateTime.UtcNow);

        Assert.Null(item.ReasonCode);
    }

    [Fact]
    public void BookingRequestDto_CanStoreRejectionCode()
    {
        var dto = new BookingRequestDto
        {
            Status = "Rejected",
            RejectionCode = "DrawNotSelected",
            RejectionReason = "Not selected in draw"
        };

        Assert.Equal("DrawNotSelected", dto.RejectionCode);
        Assert.Equal("Not selected in draw", dto.RejectionReason);
    }
}
