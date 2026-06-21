using FPS.SharedKernel.Capacity;

namespace FPS.Configuration.Tests;

// Issue #533: tests for the shared company-car capacity warning helper.
// The slot-side semantics mirror CompanyCarReservedSlotRules.Resolve from
// Booking — only active, company-car-only slots reserved for a specific user
// count as a guarantee, which is exactly what PR #529's immediate Tier 1
// allocator honors. These tests pin that mirroring so the warning never
// over-promises.
public sealed class CompanyCarCapacityCalculatorTests
{
    private const string Loc = "loc-prague";

    private static CompanyCarCapacitySlot Slot(
        string locationId = Loc,
        bool isActive = true,
        bool isCompanyCarOnly = true,
        string? reservedFor = "user-1") =>
        new(locationId, isActive, isCompanyCarOnly, reservedFor);

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
    public void IncompatibleSlot_NotCompanyCarOnly_DoesNotCount()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            // Slot is active and reserved for the user but is NOT a company-car-only
            // slot, so the allocator would not treat it as a guarantee.
            Slot(reservedFor: "user-1", isCompanyCarOnly: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
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

        // Slot counts as guaranteed capacity (it IS active+company-car+reserved)
        // but it covers user-other, not user-1. So the row still warns.
        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void UnreservedCompanyCarSlot_DoesNotCount()
    {
        var users = new Dictionary<string, IReadOnlyList<string>>
        {
            [Loc] = new[] { "user-1" },
        };
        var slots = new[]
        {
            // company-car-only but not reserved for anyone — this is a free
            // floating company-car slot, not a guarantee. Allocator would not
            // honor it as Tier 1.
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
