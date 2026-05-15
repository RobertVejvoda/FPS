using FPS.Configuration.Application;
using FPS.Configuration.Domain;

namespace FPS.Configuration.Tests;

public sealed class ParkingPolicyValidationTests
{
    private static ParkingPolicy ValidPolicy() => new()
    {
        TenantId = "tenant-1",
        TimeZone = "Europe/Prague",
        DrawCutOffTime = new TimeOnly(18, 0),
        DailyRequestCap = 100,
        AllocationLookbackDays = 10,
        LateCancellationPenalty = 1,
        NoShowPenalty = 2,
        UsageConfirmationMethods = [],
        CompanyCarOverflowBehavior = "reject",
        PublishedByUserId = "user-1",
        PublishedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Validate_ValidPolicy_ReturnsNoErrors()
    {
        var errors = ParkingPolicyService.Validate(ValidPolicy());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingTimeZone_ReturnsError()
    {
        var policy = ValidPolicy() with { TimeZone = "" };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("timeZone"));
    }

    [Fact]
    public void Validate_ZeroDailyRequestCap_ReturnsError()
    {
        var policy = ValidPolicy() with { DailyRequestCap = 0 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("dailyRequestCap"));
    }

    [Fact]
    public void Validate_NegativeDailyRequestCap_ReturnsError()
    {
        var policy = ValidPolicy() with { DailyRequestCap = -1 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("dailyRequestCap"));
    }

    [Fact]
    public void Validate_DailyRequestCapExceedsV1Limit_ReturnsError()
    {
        var policy = ValidPolicy() with { DailyRequestCap = 501 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("dailyRequestCap") && e.Contains("500"));
    }

    [Fact]
    public void Validate_NegativeAllocationLookbackDays_ReturnsError()
    {
        var policy = ValidPolicy() with { AllocationLookbackDays = -1 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("allocationLookbackDays"));
    }

    [Fact]
    public void Validate_NegativeLateCancellationPenalty_ReturnsError()
    {
        var policy = ValidPolicy() with { LateCancellationPenalty = -1 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("lateCancellationPenalty"));
    }

    [Fact]
    public void Validate_NegativeNoShowPenalty_ReturnsError()
    {
        var policy = ValidPolicy() with { NoShowPenalty = -1 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("noShowPenalty"));
    }

    [Fact]
    public void Validate_NoShowEnabledWithoutConfirmationRequired_ReturnsError()
    {
        var policy = ValidPolicy() with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationRequired = false,
            UsageConfirmationMethods = ["employee_self"]
        };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("noShowDetectionEnabled"));
    }

    [Fact]
    public void Validate_NoShowEnabledWithNoMethods_ReturnsError()
    {
        var policy = ValidPolicy() with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationRequired = true,
            UsageConfirmationMethods = []
        };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Contains(errors, e => e.Contains("noShowDetectionEnabled"));
    }

    [Fact]
    public void Validate_NoShowEnabledWithConfirmationAndMethod_ReturnsNoError()
    {
        var policy = ValidPolicy() with
        {
            NoShowDetectionEnabled = true,
            UsageConfirmationRequired = true,
            UsageConfirmationMethods = ["employee_self"]
        };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.DoesNotContain(errors, e => e.Contains("noShowDetectionEnabled"));
    }

    [Fact]
    public void Validate_MaxDailyRequestCap_ReturnsNoError()
    {
        var policy = ValidPolicy() with { DailyRequestCap = 500 };
        var errors = ParkingPolicyService.Validate(policy);
        Assert.Empty(errors);
    }
}
