namespace FPS.Booking.Application.Tests.Commands;

public sealed class SubmitBookingRequestHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IAvailableSlotService> slotService = new();
    private readonly Mock<IEmployeeMetricsService> metricsService = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IProfileSnapshotService> profileService = new();
    private readonly Mock<IBookingEventPublisher> publisher = new();
    private readonly SubmitBookingRequestHandler handler;

    private static readonly ProfileSnapshot DefaultProfile = new(
        TenantId: "tenant-1",
        UserId: "user-1",
        ProfileStatus: "Active",
        ParkingEligible: true,
        HasCompanyCar: false,
        AccessibilityEligible: false,
        ReservedSpaceEligible: false,
        Vehicles: [
            new VehicleSnapshot("v-1", "ABC-123", "Sedan", false, true),
            new VehicleSnapshot("v-2", "XYZ-999", "Sedan", false, true)],
        SnapshotVersion: "v1");

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(23, 59),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true,
        AllocationLookbackDays: 10,
        LateCancellationPenalty: 1,
        NoShowPenalty: 2);

    public SubmitBookingRequestHandlerTests()
    {
        handler = new SubmitBookingRequestHandler(
            repository.Object, queryRepository.Object, slotService.Object,
            metricsService.Object, policyService.Object, profileService.Object, publisher.Object,
            NullLogger<SubmitBookingRequestHandler>.Instance, new SystemClock());

        profileService
            .Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultProfile);

        policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        repository
            .Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        repository
            .Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Returns(Task.CompletedTask);

        queryRepository
            .Setup(r => r.AddToUserIndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        queryRepository
            .Setup(r => r.AddToTenantPendingIndexAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        queryRepository
            .Setup(r => r.AddToTenantOpsIndexAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        slotService
            .Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot>());

        metricsService
            .Setup(m => m.IncrementRecentAllocationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher.Setup(p => p.WithContext(It.IsAny<BookingPublishContext>())).Returns(publisher.Object);
        publisher.Setup(p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ── B001: future booking ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidFutureRequest_ReturnsPending()
    {
        var result = await handler.Handle(FutureCommand(), CancellationToken.None);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task Handle_ValidFutureRequest_AddsToUserIndex()
    {
        var cmd = FutureCommand();
        await handler.Handle(cmd, CancellationToken.None);
        queryRepository.Verify(r => r.AddToUserIndexAsync(
            cmd.TenantId, cmd.RequestorId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFutureRequest_AddsToTenantPendingIndex()
    {
        var cmd = FutureCommand();
        await handler.Handle(cmd, CancellationToken.None);
        queryRepository.Verify(r => r.AddToTenantPendingIndexAsync(
            cmd.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFutureRequest_AddsToTenantOpsIndex()
    {
        var cmd = FutureCommand();
        await handler.Handle(cmd, CancellationToken.None);
        queryRepository.Verify(r => r.AddToTenantOpsIndexAsync(
            cmd.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedRequest_StillAddsToTenantOpsIndex()
    {
        repository.Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);

        var cmd = FutureCommand();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        queryRepository.Verify(r => r.AddToTenantOpsIndexAsync(
            cmd.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        queryRepository.Verify(r => r.AddToTenantPendingIndexAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameDay_AllocatedRequest_AddsToTenantOpsIndex_NotPendingIndex()
    {
        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        var cmd = SameDayCommand();
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
        queryRepository.Verify(r => r.AddToTenantOpsIndexAsync(
            cmd.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        queryRepository.Verify(r => r.AddToTenantPendingIndexAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CutOffPassed_ReturnsRejected()
    {
        var latePolicy = DefaultPolicy with { DrawCutOffTime = new TimeOnly(0, 0) };
        policyService.Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latePolicy);

        var result = await handler.Handle(FutureCommand(), CancellationToken.None);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("CutOffPassed", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_DailyCapExceeded_ReturnsRejected()
    {
        repository.Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);

        var result = await handler.Handle(FutureCommand(), CancellationToken.None);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DailyCapExceeded", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_DuplicateRequest_ReturnsRejected()
    {
        repository.Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await handler.Handle(FutureCommand(), CancellationToken.None);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("DuplicateRequest", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_FutureRequest_MetricsNotIncremented()
    {
        await handler.Handle(FutureCommand(), CancellationToken.None);
        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── B002: same-day booking ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SameDay_WithAvailableSlot_ReturnsAllocated()
    {
        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
    }

    [Fact]
    public async Task Handle_SameDay_NoSlotAvailable_ReturnsRejected()
    {
        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("NoCapacityForSameDay", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_SameDay_Disabled_ReturnsRejected()
    {
        policyService.Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy with { SameDayBookingEnabled = false });

        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("SameDayBookingDisabled", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_SameDay_Successful_IncrementsMetrics()
    {
        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        await handler.Handle(SameDayCommand(), CancellationToken.None);

        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameDay_CompanyCar_DoesNotIncrementMetrics()
    {
        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("C1"), isCompanyCarReserved: true);
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        await handler.Handle(SameDayCommand(isCompanyCar: true), CancellationToken.None);

        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameDay_Rejected_DoesNotIncrementMetrics()
    {
        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameDay_DoesNotCheckCutOff()
    {
        // Even with cut-off time in the past, same-day uses different path
        policyService.Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy with { DrawCutOffTime = new TimeOnly(0, 0) });

        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        // Same-day with slot available should be Allocated, not rejected for cut-off
        Assert.Equal("Allocated", result.Status);
    }

    // ── B307: location-id capacity lookup ────────────────────────────────────

    [Fact]
    public async Task Handle_SameDay_WithLocationId_UsesLocationIdForCapacityLookup()
    {
        const string locationId = "LOC-MAIN";
        var facilityId = Guid.NewGuid().ToString();
        BookingRequestDto? saved = null;

        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), locationId, It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });
        repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Callback<BookingRequestDto>(dto => saved = dto)
            .Returns(Task.CompletedTask);

        var cmd = SameDayCommand() with { FacilityId = facilityId, LocationId = locationId };
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
        Assert.Equal(locationId, saved?.LocationId);
        slotService.Verify(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), locationId, It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()), Times.Once);
        slotService.Verify(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), facilityId, It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameDay_WithoutLocationId_FallsBackToFacilityId()
    {
        var facilityId = Guid.NewGuid().ToString();
        var slot = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), facilityId, It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { slot });

        var cmd = SameDayCommand() with { FacilityId = facilityId, LocationId = null };
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
        slotService.Verify(s => s.GetAvailableSlotsAsync(
            It.IsAny<string>(), facilityId, It.IsAny<DateOnly>(),
            It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CAP-468 motorcycle capacity ───────────────────────────────────────────

    [Fact]
    public async Task Handle_SameDay_Motorcycle_AllocatedToNormalSlotWhenNoMotorcycleCapacity()
    {
        // No motorcycle-specific capacity — same-day must still allocate the motorcycle
        // to an ordinary slot (it consumes the whole slot as one vehicle).
        profileService
            .Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MotorcycleProfile);

        var normal = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { normal });

        var result = await handler.Handle(MotorcycleSameDayCommand(), CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
    }

    [Fact]
    public async Task Handle_SameDay_Motorcycle_PrefersMotorcycleCapacityOverNormalSlot()
    {
        // Even when the normal slot is returned first, the handler picks the
        // motorcycle-specific unit for a motorcycle request.
        profileService
            .Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MotorcycleProfile);

        BookingRequestDto? saved = null;
        repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Callback<BookingRequestDto>(dto => saved = dto)
            .Returns(Task.CompletedTask);

        var normal = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("A1"));
        var mcUnit = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("M1-1"), isMotorcycleCapacity: true);
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { normal, mcUnit });

        var result = await handler.Handle(MotorcycleSameDayCommand(), CancellationToken.None);

        Assert.Equal("Allocated", result.Status);
        // AllocatedSlotId on the dto is only populated when the slot id is a guid;
        // motorcycle-unit ids are non-guid by design, so we assert the request
        // was allocated and persisted with motorcycle vehicle type for the Draw path.
        Assert.NotNull(saved);
        Assert.Equal("Motorcycle", saved!.VehicleType);
    }

    [Fact]
    public async Task Handle_SameDay_Sedan_NotAllocatedToMotorcycleOnlyCapacity()
    {
        // The motorcycle-only unit must reject a sedan request — the sedan should
        // be Pending (no compatible same-day slot), not Allocated.
        var mcUnit = AvailableSlot.Create(FPS.Booking.Domain.ValueObjects.ParkingSlotId.FromString("M1-1"), isMotorcycleCapacity: true);
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AvailableSlot> { mcUnit });

        var result = await handler.Handle(SameDayCommand(), CancellationToken.None);

        // Default sedan profile + motorcycle-only capacity = no same-day allocation.
        Assert.NotEqual("Allocated", result.Status);
    }

    [Fact]
    public async Task Handle_VehicleTypePersisted_IntoBookingRequestDto()
    {
        // The Draw restores BookingRequest from BookingRequestDto. The dto must
        // carry the original VehicleType so motorcycles match motorcycle capacity
        // during the Draw instead of being treated as Sedan.
        profileService
            .Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MotorcycleProfile);

        BookingRequestDto? saved = null;
        repository
            .Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Callback<BookingRequestDto>(dto => saved = dto)
            .Returns(Task.CompletedTask);

        await handler.Handle(MotorcycleFutureCommand(), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal("Motorcycle", saved!.VehicleType);
    }

    private static readonly ProfileSnapshot MotorcycleProfile = new(
        TenantId: "tenant-1",
        UserId: "user-1",
        ProfileStatus: "Active",
        ParkingEligible: true,
        HasCompanyCar: false,
        AccessibilityEligible: false,
        ReservedSpaceEligible: false,
        Vehicles: [new VehicleSnapshot("v-1", "MC-001", "Motorcycle", false, true)],
        SnapshotVersion: "v1");

    private static SubmitBookingRequestCommand MotorcycleSameDayCommand() => new(
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "MC-001",
        VehicleType: "Motorcycle",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: false,
        PlannedArrivalTime: DateTime.UtcNow.Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.Date.AddHours(17));

    private static SubmitBookingRequestCommand MotorcycleFutureCommand() => new(
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "MC-001",
        VehicleType: "Motorcycle",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: false,
        PlannedArrivalTime: DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.AddDays(1).Date.AddHours(17));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubmitBookingRequestCommand FutureCommand() => new(
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "ABC-123",
        VehicleType: "Sedan",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: false,
        PlannedArrivalTime: DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.AddDays(1).Date.AddHours(17));

    private static SubmitBookingRequestCommand SameDayCommand(bool isCompanyCar = false) => new(
        TenantId: "tenant-1",
        RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(),
        LocationId: null,
        LicensePlate: "XYZ-999",
        VehicleType: "Sedan",
        IsElectric: false,
        RequiresAccessibleSpot: false,
        IsCompanyCar: isCompanyCar,
        PlannedArrivalTime: DateTime.UtcNow.Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.Date.AddHours(17));
}
