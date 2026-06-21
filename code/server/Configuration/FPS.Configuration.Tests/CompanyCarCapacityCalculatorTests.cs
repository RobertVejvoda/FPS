using FPS.SharedKernel.Capacity;

namespace FPS.Configuration.Tests;

// Issue #533: tests for the shared company-car capacity warning helper.
// The slot-side semantics mirror CompanyCarReservedSlotRules.Resolve from
// Booking (FPS.Booking/Domain/Services/CompanyCarReservedSlotRules.cs). The
// allocator immediately allocates an on-time company-car request when any
// active slot reserved for that requestor can accommodate the vehicle, so the
// warning must treat such slots as guarantees regardless of the
// IsCompanyCarOnly flag. Motorcycle-only bays are the one compatibility gate
// that always rejects company cars, so they cannot count as guarantees.
public sealed class CompanyCarCapacityCalculatorTests
{
    private const string Loc = "loc-prague";

    private static CompanyCarCapacitySlot Slot(
        string locationId = Loc,
        bool isActive = true,
        bool isMotorcycleCapacity = false,
        string? reservedFor = "user-1") =>
        new(locationId, isActive, isMotorcycleCapacity, reservedFor);

    [Fact]
    public void EnoughCapacity_AllUsersReserved_NoWarning()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1", "user-2" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1"),
            Slot(reservedFor: "user-2"),
        };

        var rows = CompanyCarCapacityCalculator.Compute(users, slots);

        var row = Assert.Single(rows);
        Assert.Equal(Loc, row.LocationId);
        Assert.Equal(2, row.CompanyCarEmployeeCount);
        Assert.Equal(2, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void InsufficientCapacity_OneUserUnreserved_WarningRaised()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1", "user-2", "user-3" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1"),
            Slot(reservedFor: "user-2"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(3, row.CompanyCarEmployeeCount);
        Assert.Equal(2, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void InactiveSlot_DoesNotCountAsCapacity()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1", isActive: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void NormalReservedSlot_NotMarkedCompanyCarOnly_StillCountsAsGuarantee()
    {
        // Booking's CompanyCarReservedSlotRules.Resolve immediately allocates
        // ANY active reserved slot that the vehicle can use, regardless of the
        // IsCompanyCarOnly flag. The warning must therefore treat a normal
        // active reserved compatible slot as a guarantee — otherwise HR would
        // see a false-positive warning for an employee the allocator will
        // actually serve.
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void MotorcycleOnlySlot_DoesNotCountAsCapacity()
    {
        // A reserved motorcycle bay is rejected by the allocator for a
        // company-car vehicle (AvailableSlot.CanAccommodate), so it cannot
        // count as a company-car guarantee.
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1", isMotorcycleCapacity: true),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void SlotReservedForOtherUser_DoesNotCoverEmployee()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-other"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        // Slot counts as guaranteed capacity (it IS active + reserved + not a
        // motorcycle bay) but it covers user-other, not user-1. So the row
        // still warns.
        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void UnreservedSlot_DoesNotCount()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            // Active but not reserved for anyone — a free-floating slot that
            // the allocator will not honor as Tier 1.
            Slot(reservedFor: null),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void MultipleLocations_ComputedIndependently()
    {
        const string locA = "loc-a";
        const string locB = "loc-b";
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [locA] = new[] { "user-1", "user-2" },
            [locB] = new[] { "user-3" },
        };
        var slots = new[]
        {
            // locA: covered
            Slot(locationId: locA, reservedFor: "user-1"),
            Slot(locationId: locA, reservedFor: "user-2"),
            // locB: NOT covered (slot reserved for user-other)
            Slot(locationId: locB, reservedFor: "user-other"),
        };

        var rows = CompanyCarCapacityCalculator.Compute(users, slots).ToList();

        Assert.Equal(2, rows.Count);

        var rowA = rows.Single(r => r.LocationId == locA);
        Assert.False(rowA.IsCapacityExceeded);
        Assert.Equal(0, rowA.EmployeesWithoutGuaranteedSlot);

        var rowB = rows.Single(r => r.LocationId == locB);
        Assert.True(rowB.IsCapacityExceeded);
        Assert.Equal(1, rowB.EmployeesWithoutGuaranteedSlot);
    }

    [Fact]
    public void LocationWithNoEmployees_AndNoSlots_IsOmitted()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            ["loc-a"] = new[] { "user-1" },
        };
        var slots = new[]
        {
            Slot(locationId: "loc-a", reservedFor: "user-1"),
        };

        var rows = CompanyCarCapacityCalculator.Compute(users, slots);

        // Only loc-a should be present. loc-b is never mentioned anywhere so
        // it must not show up as a noise row.
        Assert.Equal("loc-a", Assert.Single(rows).LocationId);
    }

    [Fact]
    public void LocationWithSlotsButNoEmployees_AppearsWithZeroEmployees()
    {
        // A site that has fixed slots configured but no company-car employees
        // assigned yet should still appear so HR can verify configuration.
        var users = new Dictionary<string, IReadOnlyList<string>>();
        var slots = new[]
        {
            Slot(locationId: "loc-future", reservedFor: "user-1"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal("loc-future", row.LocationId);
        Assert.Equal(0, row.CompanyCarEmployeeCount);
        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void DuplicateUserAcrossMultipleSlots_CountsOnce()
    {
        // If HR mis-configures two reserved slots for the same user, the
        // surplus does not magically cover anyone else — slots are user-specific.
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1", "user-2" },
        };
        var slots = new[]
        {
            Slot(reservedFor: "user-1"),
            Slot(reservedFor: "user-1"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }
}
