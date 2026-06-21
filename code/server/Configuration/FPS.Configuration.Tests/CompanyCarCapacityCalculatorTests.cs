using FPS.SharedKernel.Capacity;

namespace FPS.Configuration.Tests;

// Issue #533: tests for the shared company-car capacity warning helper.
// The slot-side semantics mirror CompanyCarReservedSlotRules.Resolve +
// AvailableSlot.CanAccommodate from Booking. The allocator immediately
// allocates an on-time company-car request when any active slot reserved
// for that requestor can accommodate the vehicle. Compatibility gates:
//   * motorcycle-only bay rejects company cars
//   * electric vehicle requires a charger
//   * accessibility-eligible employee requires an accessible slot
//   * user id comparison is case-insensitive (OrdinalIgnoreCase) and trimmed
// The warning mirrors all of these so it never disagrees with the allocator.
public sealed class CompanyCarCapacityCalculatorTests
{
    private const string Loc = "loc-prague";

    private static CompanyCarCapacitySlot Slot(
        string locationId = Loc,
        bool isActive = true,
        bool isMotorcycleCapacity = false,
        bool hasCharger = false,
        bool isAccessible = false,
        string? reservedFor = "user-1") =>
        new(locationId, isActive, isMotorcycleCapacity, hasCharger, isAccessible, reservedFor);

    private static CompanyCarCapacityUser User(
        string userId,
        bool requiresChargerForEveryRequest = false,
        bool requiresAccessibleSpot = false) =>
        new(userId, requiresChargerForEveryRequest, requiresAccessibleSpot);

    private static Dictionary<string, IReadOnlyList<CompanyCarCapacityUser>> UsersAt(
        string locationId, params CompanyCarCapacityUser[] users) =>
        new() { [locationId] = users };

    [Fact]
    public void EnoughCapacity_AllUsersReserved_NoWarning()
    {
        var users = UsersAt(Loc, User("user-1"), User("user-2"));
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
        var users = UsersAt(Loc, User("user-1"), User("user-2"), User("user-3"));
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
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[] { Slot(reservedFor: "user-1", isActive: false) };

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
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[] { Slot(reservedFor: "user-1") };

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
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[] { Slot(reservedFor: "user-1", isMotorcycleCapacity: true) };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void SlotReservedForOtherUser_DoesNotCoverEmployee()
    {
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[] { Slot(reservedFor: "user-other") };

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
        var users = UsersAt(Loc, User("user-1"));
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
        var users = new Dictionary<string, IReadOnlyList<CompanyCarCapacityUser>>
        {
            [locA] = new[] { User("user-1"), User("user-2") },
            [locB] = new[] { User("user-3") },
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
        var users = UsersAt("loc-a", User("user-1"));
        var slots = new[] { Slot(locationId: "loc-a", reservedFor: "user-1") };

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
        var users = new Dictionary<string, IReadOnlyList<CompanyCarCapacityUser>>();
        var slots = new[] { Slot(locationId: "loc-future", reservedFor: "user-1") };

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
        var users = UsersAt(Loc, User("user-1"), User("user-2"));
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

    // ----- EV / accessibility compatibility mirroring AvailableSlot.CanAccommodate -----

    [Fact]
    public void ElectricVehicle_OnNonChargerReservedSlot_DoesNotCount()
    {
        // Employee has only EVs → AvailableSlot.CanAccommodate would reject a
        // non-charger slot for every request. The warning must NOT treat the
        // reservation as a guarantee, otherwise HR sees "covered" while the
        // allocator would route every request through the normal draw.
        var users = UsersAt(Loc, User("user-1", requiresChargerForEveryRequest: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", hasCharger: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);  // slot still counts in raw capacity
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void ElectricVehicle_OnChargerReservedSlot_Counts()
    {
        var users = UsersAt(Loc, User("user-1", requiresChargerForEveryRequest: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", hasCharger: true),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void MixedFleetEmployee_OnNonChargerReservedSlot_CountsAsGuarantee()
    {
        // Employee has at least one ICE option → they can request without the
        // charger constraint. A non-charger reserved slot is therefore a
        // valid guarantee (the employee picks the ICE plate at request time).
        var users = UsersAt(Loc, User("user-1", requiresChargerForEveryRequest: false));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", hasCharger: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void AccessibilityEligible_OnNonAccessibleSlot_DoesNotCount()
    {
        // Booking promotes AccessibilityEligible to VehicleInformation
        // .RequiresAccessibleSpot at request time, so a non-accessible slot
        // would be rejected by the allocator. The warning must mirror this.
        var users = UsersAt(Loc, User("user-1", requiresAccessibleSpot: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", isAccessible: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    [Fact]
    public void AccessibilityEligible_OnAccessibleSlot_Counts()
    {
        var users = UsersAt(Loc, User("user-1", requiresAccessibleSpot: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", isAccessible: true),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void EvEmployeeWithMultipleSlots_AtLeastOneChargerSlot_Counts()
    {
        // Employee is EV-only. Two slots are reserved for them: one without a
        // charger (would be rejected) and one with. The allocator picks
        // whichever is compatible, so the warning must NOT raise.
        var users = UsersAt(Loc, User("user-1", requiresChargerForEveryRequest: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", hasCharger: false),
            Slot(reservedFor: "user-1", hasCharger: true),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void EvAndAccessibilityEligible_OnEvOnlySlot_DoesNotCount()
    {
        // Both gates must pass: a slot with a charger but no accessibility
        // would still be rejected for an accessibility-eligible EV-only user.
        var users = UsersAt(Loc, User(
            "user-1",
            requiresChargerForEveryRequest: true,
            requiresAccessibleSpot: true));
        var slots = new[]
        {
            Slot(reservedFor: "user-1", hasCharger: true, isAccessible: false),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }

    // ----- User id case/whitespace normalisation mirroring AvailableSlot.IsReservedFor -----

    [Fact]
    public void ReservedUserId_DiffersOnlyInCase_StillCountsAsGuarantee()
    {
        // AvailableSlot.IsReservedFor uses StringComparison.OrdinalIgnoreCase,
        // so the allocator would honor "ABC123" reserved for the user "abc123".
        // The warning must agree to avoid false positives.
        var users = UsersAt(Loc, User("abc123"));
        var slots = new[]
        {
            Slot(reservedFor: "ABC123"),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void ReservedUserId_WithSurroundingWhitespace_StillCountsAsGuarantee()
    {
        // Mirrors AvailableSlot.NormalizeReservedForUserId which trims input.
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[]
        {
            Slot(reservedFor: "  user-1  "),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void DuplicateUsers_DifferOnlyInCase_AreDeduped()
    {
        // "USER-1" and "user-1" are the same user from the allocator's
        // perspective, so the employee count must not be inflated to 2.
        var users = UsersAt(Loc, User("user-1"), User("USER-1"));
        var slots = new[] { Slot(reservedFor: "user-1") };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(1, row.CompanyCarEmployeeCount);
        Assert.Equal(0, row.EmployeesWithoutGuaranteedSlot);
        Assert.False(row.IsCapacityExceeded);
    }

    [Fact]
    public void WhitespaceReservedUserId_IsTreatedAsUnreserved()
    {
        // A slot with reservedFor="   " is the same as a free-floating slot
        // from the allocator's perspective; it must not count as a guarantee.
        var users = UsersAt(Loc, User("user-1"));
        var slots = new[]
        {
            Slot(reservedFor: "   "),
        };

        var row = Assert.Single(CompanyCarCapacityCalculator.Compute(users, slots));

        Assert.Equal(0, row.ActiveCompatibleFixedSlotCount);
        Assert.Equal(1, row.EmployeesWithoutGuaranteedSlot);
        Assert.True(row.IsCapacityExceeded);
    }
}
