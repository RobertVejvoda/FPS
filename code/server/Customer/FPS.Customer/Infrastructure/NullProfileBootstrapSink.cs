using FPS.SharedKernel.Profile;

namespace FPS.Customer.Infrastructure;

// No-op sink used when the Profile service is not co-located in-process.
// In production, replace with an IProfileBootstrapSink that calls the Profile API.
public sealed class NullProfileBootstrapSink : IProfileBootstrapSink
{
    public Task UpsertAsync(
        string tenantId, string subjectHash, bool isActive,
        bool parkingEligible, bool hasCompanyCar, bool accessibilityEligible, bool reservedSpaceEligible,
        string factSource, CancellationToken ct) => Task.CompletedTask;
}
