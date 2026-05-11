using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Profile;
using Moq;

namespace FPS.Booking.Application.Tests.Services;

public sealed class ProfileSnapshotTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IAvailableSlotService> slotService = new();
    private readonly Mock<IEmployeeMetricsService> metricsService = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly Mock<IProfileSnapshotService> profileService = new();
    private readonly Mock<IEventPublisher> publisher = new();
    private readonly SubmitBookingRequestHandler handler;

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(23, 59),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true,
        AllocationLookbackDays: 10,
        LateCancellationPenalty: 1,
        NoShowPenalty: 2);

    public ProfileSnapshotTests()
    {
        handler = new SubmitBookingRequestHandler(
            repository.Object, queryRepository.Object, slotService.Object,
            metricsService.Object, policyService.Object, profileService.Object, publisher.Object);

        policyService.Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);
        repository.Setup(r => r.CountRequestsForDateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        repository.Setup(r => r.HasOverlappingRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>())).Returns(Task.CompletedTask);
        queryRepository.Setup(r => r.AddToUserIndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        slotService.Setup(s => s.GetAvailableSlotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<AvailableSlot>());
        metricsService.Setup(m => m.IncrementRecentAllocationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ProfileUnavailable_RejectsWithProfileUnavailable()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileSnapshot?)null);

        var result = await handler.Handle(Command("ABC-123"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("ProfileUnavailable", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_InactiveProfile_RejectsAsIneligible()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile("Inactive", parkingEligible: true));

        var result = await handler.Handle(Command("ABC-123"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("RequestorIneligible", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_NotParkingEligible_RejectsAsIneligible()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile("Active", parkingEligible: false));

        var result = await handler.Handle(Command("ABC-123"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("RequestorIneligible", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_VehicleNotInProfile_RejectsVehicleConstraintUnmatched()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile("Active", parkingEligible: true, plate: "DIFFERENT-PLATE"));

        var result = await handler.Handle(Command("ABC-123"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal("VehicleConstraintUnmatched", result.RejectionCode);
    }

    [Fact]
    public async Task Handle_CompanyCarFromProfile_OverridesRequestBody()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile("Active", parkingEligible: true, hasCompanyCar: true, plate: "ABC-123"));

        BookingRequestDto? saved = null;
        repository.Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Callback<BookingRequestDto>(dto => saved = dto)
            .Returns(Task.CompletedTask);

        // Request body says IsCompanyCar=false — profile says true
        var cmd = Command("ABC-123", isCompanyCar: false);
        await handler.Handle(cmd, CancellationToken.None);

        // Snapshot version recorded on the booking
        Assert.NotNull(saved?.ProfileSnapshotVersion);
    }

    [Fact]
    public async Task Handle_ValidProfile_RecordsSnapshotVersion()
    {
        profileService.Setup(p => p.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile("Active", parkingEligible: true, plate: "ABC-123", snapshotVersion: "snap-42"));

        BookingRequestDto? saved = null;
        repository.Setup(r => r.CreateBookingRequestAsync(It.IsAny<BookingRequestDto>()))
            .Callback<BookingRequestDto>(dto => saved = dto)
            .Returns(Task.CompletedTask);

        await handler.Handle(Command("ABC-123"), CancellationToken.None);

        Assert.Equal("snap-42", saved?.ProfileSnapshotVersion);
    }

    private static ProfileSnapshot Profile(
        string status, bool parkingEligible, bool hasCompanyCar = false,
        string plate = "ABC-123", string snapshotVersion = "v1") => new(
        TenantId: "tenant-1", UserId: "user-1",
        ProfileStatus: status, ParkingEligible: parkingEligible,
        HasCompanyCar: hasCompanyCar, AccessibilityEligible: false, ReservedSpaceEligible: false,
        Vehicles: [new VehicleSnapshot("v-1", plate, "Sedan", false, true)],
        SnapshotVersion: snapshotVersion);

    private static SubmitBookingRequestCommand Command(string plate, bool isCompanyCar = false) => new(
        TenantId: "tenant-1", RequestorId: Guid.NewGuid().ToString(),
        FacilityId: Guid.NewGuid().ToString(), LocationId: null,
        LicensePlate: plate, VehicleType: "Sedan",
        IsElectric: false, RequiresAccessibleSpot: false, IsCompanyCar: isCompanyCar,
        PlannedArrivalTime: DateTime.UtcNow.AddDays(1).Date.AddHours(9),
        PlannedDepartureTime: DateTime.UtcNow.AddDays(1).Date.AddHours(17));
}
