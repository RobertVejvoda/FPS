namespace FPS.Booking.Domain.Tests.Services;

public class DrawServiceTests
{
    private readonly DrawService _sut = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly DrawConfiguration _config = DrawConfiguration.Default();
    private readonly DateTime _drawDate = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc);

    // ── EmployeeDrawProfile ───────────────────────────────────────────────────

    [Fact]
    public void EmployeeDrawProfile_DefaultWeight_IsPriorityTier()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New(), priorityTier: 3);
        Assert.Equal(3, profile.CalculateWeight(maxFailedDrawCap: 10)); // 3 * (1 + 0)
    }

    [Fact]
    public void EmployeeDrawProfile_WeightIncreasesWithFailedDraws()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New(), priorityTier: 2);
        profile.RecordLoss(_drawDate);
        profile.RecordLoss(_drawDate);
        Assert.Equal(6, profile.CalculateWeight(maxFailedDrawCap: 10)); // 2 * (1 + 2)
    }

    [Fact]
    public void EmployeeDrawProfile_WeightCappedAtMaxFailedDrawCap()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New(), priorityTier: 1);
        for (var i = 0; i < 20; i++) profile.RecordLoss(_drawDate);
        Assert.Equal(11, profile.CalculateWeight(maxFailedDrawCap: 10)); // 1 * (1 + 10), capped
    }

    [Fact]
    public void EmployeeDrawProfile_RecordWin_ResetsConsecutiveFails()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New());
        profile.RecordLoss(_drawDate);
        profile.RecordLoss(_drawDate);
        profile.RecordWin(_drawDate);
        Assert.Equal(0, profile.ConsecutiveFailedDraws);
        Assert.Equal(1, profile.TotalWins);
        Assert.Equal(3, profile.TotalDrawsParticipated);
    }

    [Fact]
    public void EmployeeDrawProfile_IsGuaranteedWin_TrueWhenThresholdReached()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New());
        for (var i = 0; i < 15; i++) profile.RecordLoss(_drawDate);
        Assert.True(profile.IsGuaranteedWin(guaranteedWinThreshold: 15));
    }

    [Fact]
    public void EmployeeDrawProfile_IsGuaranteedWin_FalseBeforeThreshold()
    {
        var profile = EmployeeDrawProfile.Create(UserId.New());
        for (var i = 0; i < 14; i++) profile.RecordLoss(_drawDate);
        Assert.False(profile.IsGuaranteedWin(guaranteedWinThreshold: 15));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void EmployeeDrawProfile_InvalidPriorityTier_Throws(int tier)
    {
        Assert.Throws<BookingException>(() => EmployeeDrawProfile.Create(UserId.New(), tier));
    }

    // ── AvailableSlot matching ────────────────────────────────────────────────

    [Fact]
    public void AvailableSlot_CanAccommodate_ElectricVehicleNeedsCharger()
    {
        var ev = VehicleInformation.Create("EV001", VehicleType.Sedan, isElectric: true, requiresAccessibleSpot: false, isCompanyCar: false);
        var slotWithCharger = AvailableSlot.Create(Slot("A1"), hasCharger: true);
        var slotNoCharger = AvailableSlot.Create(Slot("A2"), hasCharger: false);

        Assert.True(slotWithCharger.CanAccommodate(ev));
        Assert.False(slotNoCharger.CanAccommodate(ev));
    }

    [Fact]
    public void AvailableSlot_CanAccommodate_AccessibleVehicleNeedsAccessibleSlot()
    {
        var vehicle = VehicleInformation.Create("ACC01", VehicleType.Sedan, false, requiresAccessibleSpot: true, false);
        var accessible = AvailableSlot.Create(Slot("A1"), isAccessible: true);
        var standard = AvailableSlot.Create(Slot("A2"), isAccessible: false);

        Assert.True(accessible.CanAccommodate(vehicle));
        Assert.False(standard.CanAccommodate(vehicle));
    }

    [Fact]
    public void AvailableSlot_CanAccommodate_CompanyCarGetsReservedSlot()
    {
        var companyCar = VehicleInformation.Create("CC001", VehicleType.Sedan, false, false, isCompanyCar: true);
        var reserved = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true);
        Assert.True(reserved.CanAccommodate(companyCar));
    }

    [Fact]
    public void AvailableSlot_CanAccommodate_NonCompanyCarBlockedFromReservedSlot()
    {
        var regular = VehicleInformation.Create("R001", VehicleType.Sedan, false, false, false);
        var reserved = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true);
        Assert.False(reserved.CanAccommodate(regular));
    }

    // ── DrawService: no requests / no slots ──────────────────────────────────

    [Fact]
    public void RunDraw_NoRequests_ReturnsEmptyDecisions()
    {
        var decisions = RunDraw(requests: [], slots: [Slot("A1")]);
        Assert.Empty(decisions);
    }

    [Fact]
    public void RunDraw_NoSlots_RejectsAllRequests()
    {
        var requests = new[] { MakeRequest() };
        var decisions = RunDraw(requests: requests, slots: []);

        Assert.Single(decisions);
        Assert.Equal(DrawOutcome.Rejected, decisions[0].Outcome);
    }

    // ── DrawService: Tier 1 (company car) ────────────────────────────────────

    [Fact]
    public void RunDraw_CompanyCarRequest_AllocatedBeforeRegular()
    {
        var companyCar = MakeRequest(isCompanyCar: true);
        var regular = MakeRequest();
        var slots = new[] { Slot("A1") }; // only one slot

        var decisions = RunDraw(requests: [companyCar, regular], slots: slots);

        var companyDecision = decisions.Single(d => d.RequestId == companyCar.Id);
        var regularDecision = decisions.Single(d => d.RequestId == regular.Id);

        Assert.Equal(DrawOutcome.Allocated, companyDecision.Outcome);
        Assert.Equal(DrawOutcome.Rejected, regularDecision.Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCarRequest_RejectedWhenNoSlots()
    {
        var companyCar = MakeRequest(isCompanyCar: true);
        var decisions = RunDraw(requests: [companyCar], slots: []);

        Assert.Equal(DrawOutcome.Rejected, decisions.Single().Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCarRequest_PrefersReservedSlot()
    {
        var companyCar = MakeRequest(isCompanyCar: true);
        var regularSlot = AvailableSlot.Create(Slot("A1"));
        var reservedSlot = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true);

        var decisions = RunDraw(requests: [companyCar], slots: [regularSlot.SlotId, reservedSlot.SlotId],
            slotObjects: [regularSlot, reservedSlot]);

        var decision = decisions.Single();
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.Equal(reservedSlot.SlotId, decision.SlotId);
    }

    // ── DrawService: Tier 2 (weighted lottery) ────────────────────────────────

    [Fact]
    public void RunDraw_FewerRequestsThanSlots_AllocatesAll()
    {
        var requests = new[] { MakeRequest(), MakeRequest(), MakeRequest() };
        var slots = new[] { Slot("A1"), Slot("A2"), Slot("A3"), Slot("A4") };

        var decisions = RunDraw(requests: requests, slots: slots);

        Assert.Equal(3, decisions.Count(d => d.Outcome == DrawOutcome.Allocated));
        Assert.Equal(0, decisions.Count(d => d.Outcome == DrawOutcome.Rejected));
    }

    [Fact]
    public void RunDraw_MoreRequestsThanSlots_SomeRejected()
    {
        var requests = Enumerable.Range(0, 5).Select(_ => MakeRequest()).ToArray();
        var slots = new[] { Slot("A1"), Slot("A2") };

        var decisions = RunDraw(requests: requests, slots: slots);

        Assert.Equal(2, decisions.Count(d => d.Outcome == DrawOutcome.Allocated));
        Assert.Equal(3, decisions.Count(d => d.Outcome == DrawOutcome.Rejected));
    }

    [Fact]
    public void RunDraw_EachSlotAllocatedAtMostOnce()
    {
        var requests = Enumerable.Range(0, 10).Select(_ => MakeRequest()).ToArray();
        var slots = Enumerable.Range(0, 5).Select(i => Slot($"A{i}")).ToArray();

        var decisions = RunDraw(requests: requests, slots: slots);

        var allocatedSlots = decisions
            .Where(d => d.Outcome == DrawOutcome.Allocated)
            .Select(d => d.SlotId!)
            .ToList();

        Assert.Equal(allocatedSlots.Count, allocatedSlots.Distinct().Count());
    }

    [Fact]
    public void RunDraw_EachRequestDecidedExactlyOnce()
    {
        var requests = Enumerable.Range(0, 8).Select(_ => MakeRequest()).ToArray();
        var slots = Enumerable.Range(0, 3).Select(i => Slot($"A{i}")).ToArray();

        var decisions = RunDraw(requests: requests, slots: slots);

        Assert.Equal(requests.Length, decisions.Count);
        var uniqueRequestIds = decisions.Select(d => d.RequestId).Distinct().Count();
        Assert.Equal(requests.Length, uniqueRequestIds);
    }

    [Fact]
    public void RunDraw_GuaranteedWinEmployee_AlwaysWins()
    {
        var guaranteed = UserId.New();
        var profile = EmployeeDrawProfile.Create(guaranteed);
        for (var i = 0; i < 15; i++) profile.RecordLoss(_drawDate); // hit threshold

        var guaranteedRequest = MakeRequest(userId: guaranteed);
        var others = Enumerable.Range(0, 10).Select(_ => MakeRequest()).ToArray();
        var allRequests = others.Append(guaranteedRequest).ToArray();

        var profiles = new Dictionary<UserId, EmployeeDrawProfile> { [guaranteed] = profile };
        var decisions = RunDrawWithProfiles(
            requests: allRequests,
            slots: [Slot("A1")], // only one slot — someone must lose
            profiles: profiles,
            seed: 42);

        var decision = decisions.Single(d => d.RequestId == guaranteedRequest.Id);
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
    }

    [Fact]
    public void RunDraw_HigherWeightEmployee_WinsMoreOften()
    {
        // Run draw many times with deterministic seeds and confirm high-weight wins more
        var highWeightId = UserId.New();
        var lowWeightId = UserId.New();

        int highWins = 0, lowWins = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var highProfile = EmployeeDrawProfile.Create(highWeightId, priorityTier: 5);
            for (var i = 0; i < 10; i++) highProfile.RecordLoss(_drawDate); // weight = 5 * 11 = 55

            var lowProfile = EmployeeDrawProfile.Create(lowWeightId, priorityTier: 1); // weight = 1

            var requests = new[]
            {
                MakeRequest(userId: highWeightId),
                MakeRequest(userId: lowWeightId)
            };

            var profiles = new Dictionary<UserId, EmployeeDrawProfile>
            {
                [highWeightId] = highProfile,
                [lowWeightId] = lowProfile
            };

            var decisions = RunDrawWithProfiles(requests, slots: [Slot("A1")], profiles, seed);

            if (decisions.Single(d => d.RequestorId == highWeightId).Outcome == DrawOutcome.Allocated)
                highWins++;
            else
                lowWins++;
        }

        Assert.True(highWins > lowWins * 3, $"Expected high-weight to win much more often: {highWins} vs {lowWins}");
    }

    [Fact]
    public void RunDraw_ProfilesUpdated_WinnerResetsFailCount()
    {
        var userId = UserId.New();
        var profile = EmployeeDrawProfile.Create(userId);
        profile.RecordLoss(_drawDate);
        profile.RecordLoss(_drawDate);

        var profiles = new Dictionary<UserId, EmployeeDrawProfile> { [userId] = profile };
        var request = MakeRequest(userId: userId);

        RunDrawWithProfiles([request], [Slot("A1")], profiles, seed: 0);

        Assert.Equal(0, profile.ConsecutiveFailedDraws);
        Assert.Equal(1, profile.TotalWins);
    }

    [Fact]
    public void RunDraw_ProfilesUpdated_LoserIncreasesFailCount()
    {
        var userId = UserId.New();
        var profile = EmployeeDrawProfile.Create(userId);
        var profiles = new Dictionary<UserId, EmployeeDrawProfile> { [userId] = profile };

        RunDrawWithProfiles([MakeRequest(userId: userId)], slots: [], profiles, seed: 0);

        Assert.Equal(1, profile.ConsecutiveFailedDraws);
    }

    // ── DrawService: slot compatibility in lottery ────────────────────────────

    [Fact]
    public void RunDraw_ElectricVehicle_OnlyAllocatedToChargerSlot()
    {
        var evUser = UserId.New();
        var ev = MakeRequest(userId: evUser, isElectric: true);
        var standardSlot = AvailableSlot.Create(Slot("A1"));
        var chargerSlot = AvailableSlot.Create(Slot("A2"), hasCharger: true);

        var decisions = RunDraw([ev], slots: [], slotObjects: [standardSlot, chargerSlot]);

        var decision = decisions.Single(d => d.RequestorId == evUser);
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.Equal(chargerSlot.SlotId, decision.SlotId);
    }

    [Fact]
    public void RunDraw_ElectricVehicle_RejectedWhenNoChargerSlotAvailable()
    {
        var ev = MakeRequest(isElectric: true);
        var standardSlot = AvailableSlot.Create(Slot("A1")); // no charger

        var decisions = RunDraw([ev], slots: [], slotObjects: [standardSlot]);

        Assert.Equal(DrawOutcome.Rejected, decisions.Single().Outcome);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IReadOnlyList<DrawDecision> RunDraw(
        BookingRequest[] requests,
        ParkingSlotId[] slots,
        List<AvailableSlot>? slotObjects = null,
        int seed = 0)
    {
        var availableSlots = slotObjects ?? slots.Select(s => AvailableSlot.Create(s)).ToList();
        var profiles = new Dictionary<UserId, EmployeeDrawProfile>();
        return _sut.RunDraw(requests, availableSlots, profiles, _config, _drawDate, new Random(seed));
    }

    private IReadOnlyList<DrawDecision> RunDrawWithProfiles(
        BookingRequest[] requests,
        ParkingSlotId[] slots,
        Dictionary<UserId, EmployeeDrawProfile> profiles,
        int seed = 0)
    {
        var availableSlots = slots.Select(s => AvailableSlot.Create(s)).ToList();
        return _sut.RunDraw(requests, availableSlots, profiles, _config, _drawDate, new Random(seed));
    }

    private BookingRequest MakeRequest(
        UserId? userId = null,
        bool isCompanyCar = false,
        bool isElectric = false,
        bool requiresAccessible = false)
    {
        var period = TimeSlot.Create(_drawDate.AddHours(1), _drawDate.AddHours(9));
        var vehicle = VehicleInformation.Create("X" + Guid.NewGuid().ToString("N")[..6],
            VehicleType.Sedan, isElectric, requiresAccessible, isCompanyCar);
        return BookingRequest.Create(userId ?? UserId.New(), period, vehicle, _publisher.Object);
    }

    private static ParkingSlotId Slot(string id) => ParkingSlotId.FromString(id);
}
