using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public sealed class TenantParkingBootstrapService(
    ITenantParkingBootstrapRepository bootstrapRepository,
    ITenantRepository tenantRepository)
{
    public async Task<string?> RecordDefaultPolicyAsync(
        string tenantId,
        string timeZone, string drawCutOffTime, int dailyRequestCap, int allocationLookbackDays,
        string actorHash, CancellationToken ct)
    {
        var validationError = BootstrapPolicySnapshot.Validate(timeZone, drawCutOffTime, dailyRequestCap, allocationLookbackDays);
        if (validationError is not null) return validationError;

        var tenant = await tenantRepository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived)
            return "Cannot configure parking for an archived tenant.";

        var snapshot = new BootstrapPolicySnapshot(
            timeZone.Trim(), drawCutOffTime.Trim(),
            dailyRequestCap, allocationLookbackDays,
            actorHash, DateTimeOffset.UtcNow);

        var bootstrap = await bootstrapRepository.GetOrCreateAsync(tenantId, ct);
        bootstrap.RecordDefaultPolicy(snapshot);
        await bootstrapRepository.SaveAsync(bootstrap, ct);
        return null;
    }

    public async Task<string?> RecordLocationAsync(
        string tenantId, string locationId, int activeSlotCount, bool hasLocationPolicy,
        string actorHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locationId)) return "Location ID is required.";
        if (activeSlotCount < 0) return "Active slot count cannot be negative.";

        var tenant = await tenantRepository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived)
            return "Cannot configure parking for an archived tenant.";

        var bootstrap = await bootstrapRepository.GetOrCreateAsync(tenantId, ct);
        bootstrap.RecordLocation(locationId, activeSlotCount, hasLocationPolicy, actorHash);
        await bootstrapRepository.SaveAsync(bootstrap, ct);
        return null;
    }

    public async Task<TenantParkingBootstrap> GetAsync(string tenantId, CancellationToken ct) =>
        await bootstrapRepository.GetOrCreateAsync(tenantId, ct);

    public async Task<bool> IsCompleteAsync(string tenantId, CancellationToken ct)
    {
        var bootstrap = await bootstrapRepository.GetOrCreateAsync(tenantId, ct);
        return bootstrap.IsComplete;
    }
}
