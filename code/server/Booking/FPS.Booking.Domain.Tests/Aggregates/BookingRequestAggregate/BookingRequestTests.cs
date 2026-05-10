namespace FPS.Booking.Domain.Tests.Aggregates.BookingRequestAggregate;

public class BookingRequestTests
{
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly TimeSlot _futurePeriod = TimeSlot.Create(
        DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8));

    // ── Submit — happy path ──────────────────────────────────────────────────

    [Fact]
    public void Submit_ValidFutureRequest_ReturnsPending()
    {
        var request = Submit(ValidContext());

        Assert.Equal(BookingRequestStatus.Pending, request.Status);
        Assert.Null(request.RejectionCode);
    }

    [Fact]
    public void Submit_ValidRequest_FiresSubmittedAndPendingEvents()
    {
        var request = Submit(ValidContext());

        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestSubmittedEvent>(
            e => e.RequestId == request.Id), default), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestPendingEvent>(
            e => e.RequestId == request.Id), default), Times.Once);
    }

    [Fact]
    public void Submit_ValidRequest_SetsRequestorIdAndPeriod()
    {
        var userId = UserId.New();
        var vehicle = MakeVehicle();
        var request = BookingRequest.Submit(userId, _futurePeriod, vehicle, ValidContext(), _publisher.Object);

        Assert.Equal(userId, request.RequestorId);
        Assert.Equal(_futurePeriod, request.RequestedPeriod);
        Assert.Equal(vehicle, request.Vehicle);
    }

    // ── Submit — rejection paths ─────────────────────────────────────────────

    [Fact]
    public void Submit_PastDate_ReturnsRejectedWithPastDateCode()
    {
        var pastPeriod = TimeSlot.Create(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddHours(2));
        var request = BookingRequest.Submit(UserId.New(), pastPeriod, MakeVehicle(), ValidContext(), _publisher.Object);

        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.PastDate, request.RejectionCode);
    }

    [Fact]
    public void Submit_CutOffPassed_ReturnsRejectedWithCutOffCode()
    {
        var request = Submit(ValidContext(isCutOffPassed: true));

        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.CutOffPassed, request.RejectionCode);
    }

    [Fact]
    public void Submit_DailyCapExceeded_ReturnsRejectedWithCapCode()
    {
        var request = Submit(ValidContext(cap: 10, existingCount: 10));

        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.DailyCapExceeded, request.RejectionCode);
    }

    [Fact]
    public void Submit_DuplicateRequest_ReturnsRejectedWithDuplicateCode()
    {
        var request = Submit(ValidContext(hasOverlap: true));

        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.DuplicateRequest, request.RejectionCode);
    }

    [Fact]
    public void Submit_Rejected_FiresSubmittedAndRejectedEvents()
    {
        var request = Submit(ValidContext(isCutOffPassed: true));

        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestSubmittedEvent>(
            e => e.RequestId == request.Id), default), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestRejectedEvent>(
            e => e.RequestId == request.Id && e.RejectionCode == BookingRejectionCode.CutOffPassed), default), Times.Once);
    }

    [Fact]
    public void Submit_Rejected_HasEmployeeVisibleReason()
    {
        var request = Submit(ValidContext(hasOverlap: true));

        Assert.NotNull(request.RejectionReason);
        Assert.NotEmpty(request.RejectionReason);
    }

    // ── Reject — ordering: first failing rule wins ───────────────────────────

    [Fact]
    public void Submit_PastDateTakesPrecedenceOverCutOff()
    {
        var pastPeriod = TimeSlot.Create(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddHours(2));
        var context = SubmissionContext.Create(500, 0, false, isCutOffPassed: true);
        var request = BookingRequest.Submit(UserId.New(), pastPeriod, MakeVehicle(), context, _publisher.Object);

        Assert.Equal(BookingRejectionCode.PastDate, request.RejectionCode);
    }

    // ── Allocate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Allocate_WhenPending_ChangesStatusToAllocated()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);

        Assert.Equal(BookingRequestStatus.Allocated, request.Status);
        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestAllocatedEvent>(
            e => e.RequestId == request.Id), default), Times.Once);
    }

    [Fact]
    public void Allocate_WhenNotPending_Throws()
    {
        var request = Submit(ValidContext());
        request.Reject(BookingRejectionCode.NoCapacityAvailable, "No slots", _publisher.Object);

        var ex = Assert.Throws<BookingException>(() => request.Allocate(_publisher.Object));
        Assert.Equal("Only pending requests can be allocated", ex.Message);
    }

    // ── Reject (post-Draw) ───────────────────────────────────────────────────

    [Fact]
    public void Reject_WhenPending_ChangesStatusToRejected()
    {
        var request = Submit(ValidContext());
        request.Reject(BookingRejectionCode.NoCapacityAvailable, "No slots available", _publisher.Object);

        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.NoCapacityAvailable, request.RejectionCode);
    }

    [Fact]
    public void Reject_WhenNotPending_Throws()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);

        var ex = Assert.Throws<BookingException>(() =>
            request.Reject(BookingRejectionCode.NoCapacityAvailable, "No slots", _publisher.Object));
        Assert.Equal("Only pending requests can be rejected", ex.Message);
    }

    // ── ConfirmUsage ──────────────────────────────────────────────────────────

    [Fact]
    public void ConfirmUsage_WhenAllocated_ChangesStatusToUsed()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);

        var wasNew = request.ConfirmUsage(ConfirmationSource.EmployeeSelf, DateTime.UtcNow, _publisher.Object);

        Assert.Equal(BookingRequestStatus.Used, request.Status);
        Assert.True(wasNew);
    }

    [Fact]
    public void ConfirmUsage_WhenAllocated_FiresUsedEvent()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);
        request.ConfirmUsage(ConfirmationSource.HrManual, DateTime.UtcNow, _publisher.Object);

        _publisher.Verify(p => p.PublishAsync(
            It.Is<BookingRequestUsedEvent>(e => e.RequestId == request.Id && e.Source == ConfirmationSource.HrManual),
            default), Times.Once);
    }

    [Fact]
    public void ConfirmUsage_WhenAlreadyUsed_ReturnsFalseAndNoEvent()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);
        request.ConfirmUsage(ConfirmationSource.EmployeeSelf, DateTime.UtcNow, _publisher.Object);
        _publisher.Invocations.Clear();

        var wasNew = request.ConfirmUsage(ConfirmationSource.EmployeeSelf, DateTime.UtcNow, _publisher.Object);

        Assert.False(wasNew);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public void ConfirmUsage_WhenPending_Throws()
    {
        var request = Submit(ValidContext());
        var ex = Assert.Throws<BookingException>(() =>
            request.ConfirmUsage(ConfirmationSource.EmployeeSelf, DateTime.UtcNow, _publisher.Object));
        Assert.Equal("Only allocated requests can be confirmed as used", ex.Message);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenPending_ChangesStatusToCancelled()
    {
        var request = Submit(ValidContext());
        request.Cancel("User cancelled", _publisher.Object);

        Assert.Equal(BookingRequestStatus.Cancelled, request.Status);
        _publisher.Verify(p => p.PublishAsync(It.Is<BookingRequestCancelledEvent>(
            e => e.RequestId == request.Id), default), Times.Once);
    }

    [Fact]
    public void Cancel_WhenAllocated_ChangesStatusToCancelled()
    {
        var request = Submit(ValidContext());
        request.Allocate(_publisher.Object);
        request.Cancel("Changed plans", _publisher.Object);

        Assert.Equal(BookingRequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void Cancel_WhenRejected_Throws()
    {
        var request = Submit(ValidContext(isCutOffPassed: true));

        var ex = Assert.Throws<BookingException>(() => request.Cancel("Too late", _publisher.Object));
        Assert.Equal("Only pending or allocated requests can be cancelled", ex.Message);
    }

    // ── IsTerminal ───────────────────────────────────────────────────────────

    [Fact]
    public void IsTerminal_Pending_ReturnsFalse()
        => Assert.False(Submit(ValidContext()).IsTerminal());

    [Fact]
    public void IsTerminal_Rejected_ReturnsTrue()
        => Assert.True(Submit(ValidContext(isCutOffPassed: true)).IsTerminal());

    [Fact]
    public void IsTerminal_Cancelled_ReturnsTrue()
    {
        var request = Submit(ValidContext());
        request.Cancel("Done", _publisher.Object);
        Assert.True(request.IsTerminal());
    }

    // ── Same-day validation ───────────────────────────────────────────────────

    [Fact]
    public void Submit_SameDay_WithCapacityAvailable_ReturnsPending()
    {
        var request = SubmitSameDay(sameDayEnabled: true, capacityAvailable: true);
        Assert.Equal(BookingRequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Submit_SameDay_Disabled_ReturnsRejectedWithSameDayCode()
    {
        var request = SubmitSameDay(sameDayEnabled: false, capacityAvailable: true);
        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.SameDayBookingDisabled, request.RejectionCode);
    }

    [Fact]
    public void Submit_SameDay_NoCapacity_ReturnsRejectedWithNoCapacityCode()
    {
        var request = SubmitSameDay(sameDayEnabled: true, capacityAvailable: false);
        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(BookingRejectionCode.NoCapacityForSameDay, request.RejectionCode);
    }

    [Fact]
    public void Submit_SameDay_CapExceeded_RejectsOnCap()
    {
        var ctx = SubmissionContext.CreateSameDay(10, 10, false, true, true);
        var request = BookingRequest.Submit(UserId.New(), TodayPeriod(), MakeVehicle(), ctx, _publisher.Object);
        Assert.Equal(BookingRejectionCode.DailyCapExceeded, request.RejectionCode);
    }

    [Fact]
    public void Submit_SameDay_DoesNotCheckCutOff()
    {
        // Same-day path skips cut-off check
        var request = SubmitSameDay(sameDayEnabled: true, capacityAvailable: true);
        Assert.NotEqual(BookingRejectionCode.CutOffPassed, request.RejectionCode);
    }

    // ── Restore ──────────────────────────────────────────────────────────────

    [Fact]
    public void Restore_PreservesAllFields()
    {
        var original = Submit(ValidContext());
        var restored = BookingRequest.Restore(
            original.Id, original.RequestorId, original.Vehicle,
            original.RequestedPeriod, original.Status, original.SubmittedAt);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Status, restored.Status);
        Assert.Equal(original.RequestorId, restored.RequestorId);
    }

    [Fact]
    public void Restore_PendingRequest_CanBeCancelled()
    {
        var original = Submit(ValidContext());
        var restored = BookingRequest.Restore(
            original.Id, original.RequestorId, original.Vehicle,
            original.RequestedPeriod, BookingRequestStatus.Pending, original.SubmittedAt);

        restored.Cancel("Changed plans", _publisher.Object);

        Assert.Equal(BookingRequestStatus.Cancelled, restored.Status);
    }

    [Fact]
    public void Restore_DoesNotFireEvents()
    {
        var original = Submit(ValidContext());
        _publisher.Invocations.Clear();

        BookingRequest.Restore(original.Id, original.RequestorId, original.Vehicle,
            original.RequestedPeriod, original.Status, original.SubmittedAt);

        _publisher.VerifyNoOtherCalls();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private BookingRequest Submit(SubmissionContext context)
        => BookingRequest.Submit(UserId.New(), _futurePeriod, MakeVehicle(), context, _publisher.Object);

    private BookingRequest SubmitSameDay(bool sameDayEnabled, bool capacityAvailable)
    {
        var ctx = SubmissionContext.CreateSameDay(500, 0, false, sameDayEnabled, capacityAvailable);
        return BookingRequest.Submit(UserId.New(), TodayPeriod(), MakeVehicle(), ctx, _publisher.Object);
    }

    private static TimeSlot TodayPeriod()
        => TimeSlot.Create(DateTime.UtcNow.Date.AddHours(9), DateTime.UtcNow.Date.AddHours(17));

    private static SubmissionContext ValidContext(
        int cap = 500,
        int existingCount = 0,
        bool hasOverlap = false,
        bool isCutOffPassed = false)
        => SubmissionContext.Create(cap, existingCount, hasOverlap, isCutOffPassed);

    private static VehicleInformation MakeVehicle()
        => VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, false);
}
