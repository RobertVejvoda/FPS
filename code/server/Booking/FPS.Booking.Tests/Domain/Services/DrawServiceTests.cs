namespace FPS.Booking.Domain.Tests.Services;

public sealed class DrawServiceTests
{
    private readonly DrawService sut = new();
    private readonly Mock<IEventPublisher> publisher = new();
    private readonly DateTime drawDate = new(2026, 6, 2, 18, 0, 0, DateTimeKind.Utc);

    // ── Tier 1: company-car ───────────────────────────────────────────────────

    [Fact]
    public void RunDraw_CompanyCar_WithOwnedReservedSlot_AllocatesExactSlot()
    {
        var owner = UserId.New();
        var companyCar = MakeRequest(userId: owner, isCompanyCar: true);
        var regular = MakeRequest();
        var reserved = AvailableSlot.Create(
            Slot("C1"),
            isCompanyCarReserved: true,
            reservedForUserId: owner.Value.ToString());
        var regularSlot = AvailableSlot.Create(Slot("A1"));

        var result = Run([companyCar, regular], slotObjects: [regularSlot, reserved]);

        var companyDecision = Decision(result, companyCar.Id);
        var regularDecision = Decision(result, regular.Id);
        Assert.Equal(DrawOutcome.Allocated, companyDecision.Outcome);
        Assert.Equal("C1", companyDecision.SlotId!.Value);
        Assert.Equal(DrawOutcome.Allocated, regularDecision.Outcome);
        Assert.Equal("A1", regularDecision.SlotId!.Value);
    }

    [Fact]
    public void RunDraw_CompanyCar_MissingReservedSlot_FallsThroughToNormalAllocation()
    {
        // No slot reserved for this user — company-car request joins normal Tier 2 allocation.
        // The unassigned company-car-reserved slot has no reservedForUserId, so Tier 2 considers
        // it; CanAccommodate passes for a company-car vehicle → Allocated.
        var car = MakeRequest(isCompanyCar: true);
        var unassignedCompanyCapacity = AvailableSlot.Create(Slot("CC1"), isCompanyCarReserved: true);

        var result = Run([car], slotObjects: [unassignedCompanyCapacity]);

        Assert.Equal(DrawOutcome.Allocated, result.Decisions.Single().Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCar_InactiveReservedSlot_FallsThroughToNormalAllocation_Waitlisted()
    {
        // The assigned slot is inactive — company-car request falls through to Tier 2.
        // The inactive slot has a reservedForUserId so Tier 2 skips it → Waitlisted.
        var owner = UserId.New();
        var car = MakeRequest(userId: owner, isCompanyCar: true);
        var inactiveReserved = AvailableSlot.Create(
            Slot("C1"),
            isActive: false,
            isCompanyCarReserved: true,
            reservedForUserId: owner.Value.ToString());

        var result = Run([car], slotObjects: [inactiveReserved]);

        Assert.Equal(DrawOutcome.Waitlisted, result.Decisions.Single().Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCar_IncompatibleReservedSlot_FallsThroughToNormalAllocation_Waitlisted()
    {
        // The assigned slot lacks EV charging — company-car request falls through to Tier 2.
        // The slot has a reservedForUserId so Tier 2 skips it → Waitlisted.
        var owner = UserId.New();
        var evCompanyCar = MakeRequest(userId: owner, isCompanyCar: true, isElectric: true);
        var reservedWithoutCharger = AvailableSlot.Create(
            Slot("C1"),
            isCompanyCarReserved: true,
            reservedForUserId: owner.Value.ToString());

        var result = Run([evCompanyCar], slotObjects: [reservedWithoutCharger]);

        Assert.Equal(DrawOutcome.Waitlisted, result.Decisions.Single().Outcome);
    }

    [Fact]
    public void RunDraw_Tier2_DoesNotConsumeReservedSlots()
    {
        var regular = MakeRequest();
        var reserved = AvailableSlot.Create(
            Slot("C1"),
            isCompanyCarReserved: true,
            reservedForUserId: UserId.New().Value.ToString());

        var result = Run([regular], slotObjects: [reserved]);

        Assert.Equal(DrawOutcome.Waitlisted, Decision(result, regular.Id).Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCar_SameOwnerSecondRequest_SlotConsumed_SecondWaitlisted()
    {
        // First request consumes the fixed slot; second request falls through to Tier 2.
        // No other slots remain → Waitlisted (not Rejected).
        var owner = UserId.New();
        var first = MakeRequest(userId: owner, isCompanyCar: true);
        var second = MakeRequest(userId: owner, isCompanyCar: true);
        var reserved = AvailableSlot.Create(
            Slot("C1"),
            isCompanyCarReserved: true,
            reservedForUserId: owner.Value.ToString());

        var result = Run([first, second], slotObjects: [reserved]);

        Assert.Equal(DrawOutcome.Allocated, Decision(result, first.Id).Outcome);
        Assert.Equal(DrawOutcome.Waitlisted, Decision(result, second.Id).Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCar_Tier1FixedSlotAllocation_IsTier1Guaranteed()
    {
        var owner = UserId.New();
        var companyCar = MakeRequest(userId: owner, isCompanyCar: true);
        var reserved = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true, reservedForUserId: owner.Value.ToString());

        var result = Run([companyCar], slotObjects: [reserved]);

        var decision = result.Decisions.Single();
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.True(decision.IsTier1Guaranteed);
    }

    [Fact]
    public void RunDraw_CompanyCar_FallbackTier2Allocation_IsNotTier1Guaranteed()
    {
        // No fixed slot for this user; they enter the Tier 2 lottery and win.
        var car = MakeRequest(isCompanyCar: true);
        var normalSlot = AvailableSlot.Create(Slot("A1"));

        var result = Run([car], slotObjects: [normalSlot]);

        var decision = result.Decisions.Single();
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.False(decision.IsTier1Guaranteed);
    }

    [Fact]
    public void RunDraw_RegularTier2Allocation_IsNotTier1Guaranteed()
    {
        var regular = MakeRequest();
        var result = Run([regular], [Slot("A1")]);

        Assert.False(result.Decisions.Single().IsTier1Guaranteed);
    }

    // ── Tier 2: weighted lottery ──────────────────────────────────────────────

    [Fact]
    public void RunDraw_FewerRequestsThanSlots_AllocatesAll()
    {
        var requests = Enumerable.Range(0, 3).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, [Slot("A1"), Slot("A2"), Slot("A3"), Slot("A4")]);

        Assert.All(result.Decisions, d => Assert.Equal(DrawOutcome.Allocated, d.Outcome));
    }

    [Fact]
    public void RunDraw_MoreRequestsThanSlots_ExcessWaitlisted()
    {
        var requests = Enumerable.Range(0, 5).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, [Slot("A1"), Slot("A2")]);

        Assert.Equal(2, result.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated));
        Assert.Equal(3, result.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted));
        Assert.Equal(0, result.Decisions.Count(d => d.Outcome == DrawOutcome.Rejected));
    }

    [Fact]
    public void RunDraw_CapacityExhaustedLosers_AreWaitlistedNotRejected()
    {
        var requests = Enumerable.Range(0, 4).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, [Slot("A1")]);

        Assert.DoesNotContain(result.Decisions, d => d.Outcome == DrawOutcome.Rejected);
        Assert.Equal(3, result.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted));
    }

    [Fact]
    public void RunDraw_Deterministic_SameSeedSameOutcome()
    {
        var requests = Enumerable.Range(0, 6).Select(_ => MakeRequest()).ToArray();
        var slots = Enumerable.Range(0, 3).Select(i => Slot($"A{i}")).ToArray();
        var metrics = EmptyMetrics(requests);

        var result1 = sut.RunDraw(requests, slots.Select(s => AvailableSlot.Create(s)).ToList(), metrics, seed: 42);
        var result2 = sut.RunDraw(requests, slots.Select(s => AvailableSlot.Create(s)).ToList(), metrics, seed: 42);

        var winners1 = result1.Decisions.Where(d => d.Outcome == DrawOutcome.Allocated).Select(d => d.RequestorId).OrderBy(x => x.Value).ToList();
        var winners2 = result2.Decisions.Where(d => d.Outcome == DrawOutcome.Allocated).Select(d => d.RequestorId).OrderBy(x => x.Value).ToList();

        Assert.Equal(winners1, winners2);
    }

    [Fact]
    public void RunDraw_DifferentSeeds_CanProduceDifferentOutcomes()
    {
        var requests = Enumerable.Range(0, 10).Select(_ => MakeRequest()).ToArray();
        var slots = Enumerable.Range(0, 3).Select(i => Slot($"A{i}")).ToArray();
        var metrics = EmptyMetrics(requests);

        var seen = new HashSet<string>();
        for (var seed = 0; seed < 50; seed++)
        {
            var result = sut.RunDraw(requests, slots.Select(s => AvailableSlot.Create(s)).ToList(), metrics, seed);
            seen.Add(string.Join(",", result.Decisions.Where(d => d.Outcome == DrawOutcome.Allocated).Select(d => d.RequestorId.Value)));
        }

        Assert.True(seen.Count > 1, "Expected different seeds to produce different winner sets");
    }

    // ── Weight formula ────────────────────────────────────────────────────────

    [Fact]
    public void RunDraw_HigherRecentAllocationCount_LowerWinProbability()
    {
        var highCountId = UserId.New();
        var lowCountId = UserId.New();

        int highWins = 0, lowWins = 0;

        for (var seed = 0; seed < 300; seed++)
        {
            var highMetrics = new EmployeeMetrics(highCountId.Value.ToString(), RecentAllocationCount: 9, ActivePenaltyScore: 0);
            var lowMetrics = new EmployeeMetrics(lowCountId.Value.ToString(), RecentAllocationCount: 0, ActivePenaltyScore: 0);

            var requests = new[] { MakeRequest(userId: highCountId), MakeRequest(userId: lowCountId) };
            var metrics = new Dictionary<string, EmployeeMetrics>
            {
                [highCountId.Value.ToString()] = highMetrics,
                [lowCountId.Value.ToString()] = lowMetrics
            };

            var result = sut.RunDraw(requests, [AvailableSlot.Create(Slot("A1"))], metrics, seed);

            if (Decision(result, requests[0].Id).Outcome == DrawOutcome.Allocated) highWins++;
            else lowWins++;
        }

        // lowCountId weight = 1/(1+0+0) = 1.0
        // highCountId weight = 1/(1+9+0) = 0.1
        // lowCountId should win ~90% of the time
        Assert.True(lowWins > highWins * 3, $"Low-count employee should win more often: {lowWins} vs {highWins}");
    }

    [Fact]
    public void RunDraw_ActivePenalty_ReducesWinProbability()
    {
        var penalisedId = UserId.New();
        var cleanId = UserId.New();

        int penalisedWins = 0, cleanWins = 0;

        for (var seed = 0; seed < 300; seed++)
        {
            var requests = new[] { MakeRequest(userId: penalisedId), MakeRequest(userId: cleanId) };
            var metrics = new Dictionary<string, EmployeeMetrics>
            {
                [penalisedId.Value.ToString()] = new(penalisedId.Value.ToString(), 0, ActivePenaltyScore: 9),
                [cleanId.Value.ToString()] = new(cleanId.Value.ToString(), 0, 0)
            };

            var result = sut.RunDraw(requests, [AvailableSlot.Create(Slot("A1"))], metrics, seed);

            if (Decision(result, requests[0].Id).Outcome == DrawOutcome.Allocated) penalisedWins++;
            else cleanWins++;
        }

        Assert.True(cleanWins > penalisedWins * 3, $"Clean employee should win more often: {cleanWins} vs {penalisedWins}");
    }

    // ── Slot matching ─────────────────────────────────────────────────────────

    [Fact]
    public void RunDraw_ElectricVehicle_OnlyAllocatedToChargerSlot()
    {
        var ev = MakeRequest(isElectric: true);
        var standardSlot = AvailableSlot.Create(Slot("A1"));
        var chargerSlot = AvailableSlot.Create(Slot("A2"), hasCharger: true);

        var result = Run([ev], slotObjects: [standardSlot, chargerSlot]);

        var decision = result.Decisions.Single();
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.Equal(chargerSlot.SlotId, decision.SlotId);
    }

    [Fact]
    public void RunDraw_ElectricVehicle_WaitlistedWhenNoCharger()
    {
        var ev = MakeRequest(isElectric: true);
        var standardSlot = AvailableSlot.Create(Slot("A1"));

        var result = Run([ev], slotObjects: [standardSlot]);

        Assert.Equal(DrawOutcome.Waitlisted, result.Decisions.Single().Outcome);
    }

    // ── Invariants ────────────────────────────────────────────────────────────

    [Fact]
    public void RunDraw_EachRequestDecidedExactlyOnce()
    {
        var requests = Enumerable.Range(0, 8).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, Enumerable.Range(0, 3).Select(i => Slot($"A{i}")).ToArray());

        Assert.Equal(requests.Length, result.Decisions.Count);
        Assert.Equal(requests.Length, result.Decisions.Select(d => d.RequestId).Distinct().Count());
    }

    [Fact]
    public void RunDraw_EachSlotAssignedAtMostOnce()
    {
        var requests = Enumerable.Range(0, 10).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, Enumerable.Range(0, 5).Select(i => Slot($"A{i}")).ToArray());

        var assignedSlots = result.Decisions
            .Where(d => d.SlotId is not null)
            .Select(d => d.SlotId!)
            .ToList();

        Assert.Equal(assignedSlots.Count, assignedSlots.Distinct().Count());
    }

    [Fact]
    public void RunDraw_NoRequests_ReturnsEmptyDecisions()
    {
        var result = Run([], [Slot("A1")]);
        Assert.Empty(result.Decisions);
    }

    [Fact]
    public void RunDraw_NoSlots_WaitlistsAllTier2()
    {
        var requests = Enumerable.Range(0, 3).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, []);

        Assert.All(result.Decisions, d => Assert.Equal(DrawOutcome.Waitlisted, d.Outcome));
    }

    [Fact]
    public void RunDraw_StoresTier2CandidateSequence()
    {
        var requests = Enumerable.Range(0, 5).Select(_ => MakeRequest()).ToArray();
        var result = Run(requests, [Slot("A1")]);

        Assert.Equal(5, result.Tier2CandidateSequence.Count);
        Assert.All(requests, r => Assert.Contains(r.Id, result.Tier2CandidateSequence));
    }

    [Fact]
    public void RunDraw_StoresAlgorithmVersionAndSeed()
    {
        var result = Run([MakeRequest()], [Slot("A1")], seed: 99);

        Assert.Equal(99, result.Seed);
        Assert.NotEmpty(result.AlgorithmVersion);
    }

    // ── Motorcycle capacity (CAP-468) ─────────────────────────────────────────

    [Fact]
    public void RunDraw_Motorcycle_NoMotorcycleSlot_AllocatesToNormalSlot()
    {
        // No motorcycle-specific capacity exists — the motorcycle takes a normal slot.
        // It consumes the whole slot as one vehicle.
        var motorcycle = MakeRequest(vehicleType: VehicleType.Motorcycle);
        var result = Run([motorcycle], [Slot("A1")]);

        Assert.Equal(DrawOutcome.Allocated, Decision(result, motorcycle.Id).Outcome);
    }

    [Fact]
    public void RunDraw_Motorcycle_PrefersMotorcycleSlot_OverNormalSlot()
    {
        // Preference rule: motorcycle-specific capacity should be consumed first.
        var motorcycle = MakeRequest(vehicleType: VehicleType.Motorcycle);
        var normal = AvailableSlot.Create(Slot("A1"));
        var mcSlot = AvailableSlot.Create(Slot("M1"), isMotorcycleCapacity: true);

        // Order normal first to prove the algorithm reorders to motorcycle-first.
        var result = Run([motorcycle], slotObjects: [normal, mcSlot]);

        var decision = Decision(result, motorcycle.Id);
        Assert.Equal(DrawOutcome.Allocated, decision.Outcome);
        Assert.Equal("M1", decision.SlotId!.Value);
    }

    [Fact]
    public void RunDraw_Sedan_DoesNotConsumeMotorcycleSlot()
    {
        // Sedan can't use a motorcycle-only slot — it should be waitlisted when that's all there is.
        var sedan = MakeRequest(vehicleType: VehicleType.Sedan);
        var mcSlot = AvailableSlot.Create(Slot("M1"), isMotorcycleCapacity: true);

        var result = Run([sedan], slotObjects: [mcSlot]);

        Assert.Equal(DrawOutcome.Waitlisted, Decision(result, sedan.Id).Outcome);
    }

    [Fact]
    public void RunDraw_FourMotorcyclesOnMultiUnitArea_AllAllocatedSeparately()
    {
        // The capacity loader expands a 4-unit motorcycle area into four AvailableSlot
        // instances with suffixed IDs. The Draw treats each unit as a normal allocatable
        // slot — four motorcycles fill the area, a fifth is waitlisted.
        var motorcycles = Enumerable.Range(0, 5)
            .Select(_ => MakeRequest(vehicleType: VehicleType.Motorcycle))
            .ToArray();
        var unitSlots = Enumerable.Range(1, 4)
            .Select(unit => AvailableSlot.Create(Slot($"M1-{unit}"), isMotorcycleCapacity: true))
            .ToList();

        var result = Run(motorcycles, slotObjects: unitSlots);

        Assert.Equal(4, result.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated));
        Assert.Equal(1, result.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted));
    }

    [Fact]
    public void RunDraw_MotorcycleOverflow_FallsBackToNormalSlot()
    {
        // 3 motorcycles, motorcycle-area has only 2 units → the overflow motorcycle
        // takes a normal slot (motorcycles can use ordinary slots).
        var motorcycles = Enumerable.Range(0, 3)
            .Select(_ => MakeRequest(vehicleType: VehicleType.Motorcycle))
            .ToArray();
        var mcUnits = new[]
        {
            AvailableSlot.Create(Slot("M1-1"), isMotorcycleCapacity: true),
            AvailableSlot.Create(Slot("M1-2"), isMotorcycleCapacity: true),
        };
        var normal = AvailableSlot.Create(Slot("A1"));

        var result = Run(motorcycles, slotObjects: [.. mcUnits, normal]);

        Assert.Equal(3, result.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated));
        var allocatedSlots = result.Decisions
            .Where(d => d.Outcome == DrawOutcome.Allocated)
            .Select(d => d.SlotId!.Value)
            .ToHashSet();
        Assert.Contains("M1-1", allocatedSlots);
        Assert.Contains("M1-2", allocatedSlots);
        Assert.Contains("A1", allocatedSlots);
    }

    [Fact]
    public void RunDraw_MotorcyclesAndCars_CarsDoNotConsumeMotorcycleUnits()
    {
        // 1 motorcycle, 1 sedan; one motorcycle-unit and one normal slot.
        // Motorcycle takes the motorcycle unit, sedan takes the normal slot.
        // The motorcycle unit must not be allocated to the sedan.
        var motorcycle = MakeRequest(vehicleType: VehicleType.Motorcycle);
        var sedan = MakeRequest(vehicleType: VehicleType.Sedan);
        var mcUnit = AvailableSlot.Create(Slot("M1-1"), isMotorcycleCapacity: true);
        var normal = AvailableSlot.Create(Slot("A1"));

        var result = Run([motorcycle, sedan], slotObjects: [mcUnit, normal]);

        Assert.Equal("M1-1", Decision(result, motorcycle.Id).SlotId!.Value);
        Assert.Equal("A1", Decision(result, sedan.Id).SlotId!.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DrawResult Run(
        BookingRequest[] requests,
        ParkingSlotId[]? slots = null,
        List<AvailableSlot>? slotObjects = null,
        long seed = 0)
    {
        var available = slotObjects ?? slots?.Select(s => AvailableSlot.Create(s)).ToList() ?? [];
        return sut.RunDraw(requests, available, EmptyMetrics(requests), seed);
    }

    private static Dictionary<string, EmployeeMetrics> EmptyMetrics(BookingRequest[] requests)
        => requests
            .Select(r => r.RequestorId.Value.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                requestorId => requestorId,
                requestorId => new EmployeeMetrics(requestorId, 0, 0),
                StringComparer.OrdinalIgnoreCase);

    private BookingRequest MakeRequest(
        UserId? userId = null,
        bool isCompanyCar = false,
        bool isElectric = false,
        bool requiresAccessible = false,
        VehicleType vehicleType = VehicleType.Sedan)
    {
        var period = TimeSlot.Create(drawDate.AddHours(1), drawDate.AddHours(9));
        var vehicle = VehicleInformation.Create(
            "X" + Guid.NewGuid().ToString("N")[..6],
            vehicleType, isElectric, requiresAccessible, isCompanyCar);
        return BookingRequest.Submit(userId ?? UserId.New(), period, vehicle,
            SubmissionContext.Create(500, 0, false, false), publisher.Object);
    }

    private static ParkingSlotId Slot(string id) => ParkingSlotId.FromString(id);

    private static DrawDecision Decision(DrawResult result, BookingRequestId id)
        => result.Decisions.Single(d => d.RequestId == id);
}
