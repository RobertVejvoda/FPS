using FPS.Booking.Application.Services;
using FPS.Booking.Infrastructure.Services;

namespace FPS.Booking.Application.Tests.Services;

public sealed class TenantPolicyTests
{
    // --- Spec defaults ---

    [Fact]
    public void Default_DailyRequestCap_Is500()
        => Assert.Equal(500, DefaultTenantPolicyService.Default.DailyRequestCap);

    [Fact]
    public void Default_AllocationLookbackDays_Is10()
        => Assert.Equal(10, DefaultTenantPolicyService.Default.AllocationLookbackDays);

    [Fact]
    public void Default_LateCancellationPenalty_Is1()
        => Assert.Equal(1, DefaultTenantPolicyService.Default.LateCancellationPenalty);

    [Fact]
    public void Default_NoShowPenalty_Is2()
        => Assert.Equal(2, DefaultTenantPolicyService.Default.NoShowPenalty);

    [Fact]
    public void Default_SameDayBookingEnabled_IsTrue()
        => Assert.True(DefaultTenantPolicyService.Default.SameDayBookingEnabled);

    [Fact]
    public void Default_SameDayUsesRequestCap_IsTrue()
        => Assert.True(DefaultTenantPolicyService.Default.SameDayUsesRequestCap);

    [Fact]
    public void Default_AutomaticReallocationEnabled_IsTrue()
        => Assert.True(DefaultTenantPolicyService.Default.AutomaticReallocationEnabled);

    [Fact]
    public void Default_CompanyCarTier1Enabled_IsTrue()
        => Assert.True(DefaultTenantPolicyService.Default.CompanyCarTier1Enabled);

    [Fact]
    public void Default_CompanyCarOverflow_IsReject()
        => Assert.Equal(CompanyCarOverflow.Reject, DefaultTenantPolicyService.Default.CompanyCarOverflowBehavior);

    [Fact]
    public void Default_ManualAdjustmentEnabled_IsTrue()
        => Assert.True(DefaultTenantPolicyService.Default.ManualAdjustmentEnabled);

    [Fact]
    public void Default_UsageConfirmationEnabled_IsFalse()
        => Assert.False(DefaultTenantPolicyService.Default.UsageConfirmationEnabled);

    [Fact]
    public void Default_NoShowDetectionEnabled_IsFalse()
        => Assert.False(DefaultTenantPolicyService.Default.NoShowDetectionEnabled);

    // --- Penalty expiry defaults to lookback window ---

    [Fact]
    public void EffectivePenaltyExpiry_WhenNull_UsesLookbackWindow()
    {
        var policy = DefaultTenantPolicyService.Default;

        Assert.Null(policy.LateCancellationPenaltyExpiryDays);
        Assert.Null(policy.NoShowPenaltyExpiryDays);
        Assert.Equal(policy.AllocationLookbackDays, policy.EffectiveLateCancellationPenaltyExpiry);
        Assert.Equal(policy.AllocationLookbackDays, policy.EffectiveNoShowPenaltyExpiry);
    }

    [Fact]
    public void EffectivePenaltyExpiry_WhenExplicit_UsesExplicitValue()
    {
        var policy = DefaultTenantPolicyService.Default with
        {
            LateCancellationPenaltyExpiryDays = 30,
            NoShowPenaltyExpiryDays = 60
        };

        Assert.Equal(30, policy.EffectiveLateCancellationPenaltyExpiry);
        Assert.Equal(60, policy.EffectiveNoShowPenaltyExpiry);
    }

    // --- Location override merge ---

    [Fact]
    public void WithLocationOverride_Null_ReturnsSamePolicy()
    {
        var policy = DefaultTenantPolicyService.Default;
        Assert.Same(policy, policy.WithLocationOverride(null));
    }

    [Fact]
    public void WithLocationOverride_OverriddenFields_WinOverTenantDefault()
    {
        var policy = DefaultTenantPolicyService.Default;
        var loc = new LocationPolicyOverride(
            DailyRequestCap: 100,
            DrawCutOffTime: new TimeOnly(16, 0),
            SameDayBookingEnabled: false);

        var effective = policy.WithLocationOverride(loc);

        Assert.Equal(100, effective.DailyRequestCap);
        Assert.Equal(new TimeOnly(16, 0), effective.DrawCutOffTime);
        Assert.False(effective.SameDayBookingEnabled);
    }

    [Fact]
    public void WithLocationOverride_UnsetFields_FallBackToTenantDefault()
    {
        var policy = DefaultTenantPolicyService.Default;
        var loc = new LocationPolicyOverride(DailyRequestCap: 100);

        var effective = policy.WithLocationOverride(loc);

        // Non-overridden fields are unchanged
        Assert.Equal(policy.AllocationLookbackDays, effective.AllocationLookbackDays);
        Assert.Equal(policy.LateCancellationPenalty, effective.LateCancellationPenalty);
        Assert.Equal(policy.NoShowPenalty, effective.NoShowPenalty);
        Assert.Equal(policy.CompanyCarTier1Enabled, effective.CompanyCarTier1Enabled);
    }

    // --- Defaults for new fields ---

    [Fact]
    public void Default_DrawCutOffTime_Is18h00()
        => Assert.Equal(new TimeOnly(18, 0), DefaultTenantPolicyService.Default.DrawCutOffTime);

    [Fact]
    public void Default_UsageConfirmationMethods_IsNullOrEmpty()
        => Assert.False(DefaultTenantPolicyService.Default.HasConfirmationMethod);

    // --- Validation ---

    [Fact]
    public void Validate_NoShowEnabledWithoutConfirmationMethod_Throws()
    {
        var policy = DefaultTenantPolicyService.Default with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationMethods = null
        };

        Assert.Throws<InvalidOperationException>(policy.Validate);
    }

    [Fact]
    public void Validate_NoShowEnabledWithEmptyConfirmationMethods_Throws()
    {
        var policy = DefaultTenantPolicyService.Default with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationMethods = []
        };

        Assert.Throws<InvalidOperationException>(policy.Validate);
    }

    [Fact]
    public void Validate_NoShowEnabled_MethodsPresent_ButConfirmationDisabled_Throws()
    {
        // Having methods without UsageConfirmationEnabled=true is not sufficient
        var policy = DefaultTenantPolicyService.Default with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationEnabled = false,
            UsageConfirmationMethods = ["self-confirmation"]
        };

        Assert.Throws<InvalidOperationException>(policy.Validate);
    }

    [Fact]
    public void Validate_NoShowEnabledWithConfirmationEnabledAndMethod_DoesNotThrow()
    {
        var policy = DefaultTenantPolicyService.Default with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationEnabled = true,
            UsageConfirmationMethods = ["self-confirmation"]
        };

        policy.Validate(); // no exception
    }

    [Fact]
    public void WithLocationOverride_UsageConfirmationMethods_OverridesWhenSet()
    {
        var policy = DefaultTenantPolicyService.Default;
        var loc = new LocationPolicyOverride(UsageConfirmationMethods: ["qr-code", "card-reader"]);

        var effective = policy.WithLocationOverride(loc);

        Assert.Equal(2, effective.UsageConfirmationMethods!.Count);
        Assert.True(effective.HasConfirmationMethod);
    }

    [Fact]
    public void Validate_DailyRequestCapZero_Throws()
    {
        var policy = DefaultTenantPolicyService.Default with { DailyRequestCap = 0 };
        Assert.Throws<InvalidOperationException>(policy.Validate);
    }

    [Fact]
    public void Validate_AllocationLookbackDaysZero_Throws()
    {
        var policy = DefaultTenantPolicyService.Default with { AllocationLookbackDays = 0 };
        Assert.Throws<InvalidOperationException>(policy.Validate);
    }

    [Fact]
    public void Validate_DefaultPolicy_IsValid()
    {
        DefaultTenantPolicyService.Default.Validate(); // no exception
    }
}
