using FPS.Configuration.Domain;

namespace FPS.Configuration.Application;

public sealed class ParkingPolicyService(IParkingPolicyRepository repository)
{
    public Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken ct)
        => repository.GetTenantDefaultAsync(tenantId, ct);

    public async Task<ParkingPolicy?> GetEffectivePolicyAsync(string tenantId, string locationId, CancellationToken ct)
    {
        var locationOverride = await repository.GetLocationOverrideAsync(tenantId, locationId, ct);
        return locationOverride ?? await repository.GetTenantDefaultAsync(tenantId, ct);
    }

    public async Task<IReadOnlyList<string>> SaveTenantDefaultAsync(ParkingPolicy policy, CancellationToken ct)
    {
        var errors = Validate(policy);
        if (errors.Count > 0) return errors;
        await repository.SaveAsync(policy, ct);
        return [];
    }

    public async Task<IReadOnlyList<string>> SaveLocationOverrideAsync(ParkingPolicy policy, CancellationToken ct)
    {
        var errors = Validate(policy);
        if (errors.Count > 0) return errors;
        await repository.SaveAsync(policy, ct);
        return [];
    }

    public static IReadOnlyList<string> Validate(ParkingPolicy policy)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(policy.TimeZone))
            errors.Add("timeZone is required.");

        if (policy.DailyRequestCap <= 0)
            errors.Add("dailyRequestCap must be greater than zero.");

        if (policy.DailyRequestCap > ParkingPolicy.V1DailyRequestCapLimit)
            errors.Add($"dailyRequestCap exceeds the v1 limit of {ParkingPolicy.V1DailyRequestCapLimit}.");

        if (policy.AllocationLookbackDays < 0)
            errors.Add("allocationLookbackDays must be non-negative.");

        if (policy.LateCancellationPenalty < 0)
            errors.Add("lateCancellationPenalty must be non-negative.");

        if (policy.NoShowPenalty < 0)
            errors.Add("noShowPenalty must be non-negative.");

        if (policy.NoShowDetectionEnabled &&
            (!policy.UsageConfirmationRequired || policy.UsageConfirmationMethods.Count == 0))
        {
            errors.Add("noShowDetectionEnabled requires usageConfirmationRequired=true and at least one usageConfirmationMethod.");
        }

        return errors;
    }
}
