namespace FPS.Booking.Domain.Tests.Services;

public sealed class DrawServiceTests
{
    private readonly DrawService sut = new();
    private readonly Mock<IEventPublisher> publisher = new();
    private readonly DateTime drawDate = new(2026, 6, 2, 18, 0, 0, DateTimeKind.Utc);

    // ── Tier 1: company-car ───────────────────────────────────────────────────

    [Fact]
    public void RunDraw_CompanyCar_AllocatedBeforeTier2()
    {
        var companyCar = MakeRequest(isCompanyCar: true);
        var regular = MakeRequest();
        var slots = new[] { Slot("A1") }; // one slot only

        var result = Run([companyCar, regular], slots);

        Assert.Equal(DrawOutcome.Allocated, Decision(result, companyCar.Id).Outcome);
        Assert.Equal(DrawOutcome.Waitlisted, Decision(result, regular.Id).Outcome);
    }

    [Fact]
    public void RunDraw_CompanyCarOverflow_RejectsExcess()
    {
        var car1 = MakeRequest(isCompanyCar: true);
        var car2 = MakeRequest(isCompanyCar: true);
        var reservedSlot = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true);

        var result = Run([car1, car2], slotObjects: [reservedSlot]);

        var outcomes = result.Decisions.Select(d => d.Outcome).ToList();
        Assert.Contains(DrawOutcome.Allocated, outcomes);
        Assert.Contains(DrawOutcome.Rejected, outcomes);
    }

    [Fact]
    public void RunDraw_CompanyCarOverflow_RejectionReasonExplainsConfigurationDrift()
    {
        var car1 = MakeRequest(isCompanyCar: true);
        var car2 = MakeRequest(isCompanyCar: true);
        var reservedSlot = AvailableSlot.Create(Slot("C1"), isCompanyCarReserved: true);

        var result = Run([car1, car2], slotObjects: [reservedSlot]);

        var rejected = result.Decisions.Single(d => d.Outcome == DrawOutcome.Rejected);
        Assert.NotNull(rejected.Reason);
        Assert.NotEmpty(rejected.Reason!);
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
        => requests.ToDictionary(
            r => r.RequestorId.Value.ToString(),
            r => new EmployeeMetrics(r.RequestorId.Value.ToString(), 0, 0));

    private BookingRequest MakeRequest(
        UserId? userId = null,
        bool isCompanyCar = false,
        bool isElectric = false,
        bool requiresAccessible = false)
    {
        var period = TimeSlot.Create(drawDate.AddHours(1), drawDate.AddHours(9));
        var vehicle = VehicleInformation.Create(
            "X" + Guid.NewGuid().ToString("N")[..6],
            VehicleType.Sedan, isElectric, requiresAccessible, isCompanyCar);
        return BookingRequest.Submit(userId ?? UserId.New(), period, vehicle,
            SubmissionContext.Create(500, 0, false, false), publisher.Object);
    }

    private static ParkingSlotId Slot(string id) => ParkingSlotId.FromString(id);

    private static DrawDecision Decision(DrawResult result, BookingRequestId id)
        => result.Decisions.Single(d => d.RequestId == id);
}
