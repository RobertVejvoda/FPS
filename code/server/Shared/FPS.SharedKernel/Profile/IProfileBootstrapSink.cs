namespace FPS.SharedKernel.Profile;

// Allows the Customer onboarding service to write minimal pilot-user profile facts
// into the Profile service's store so Booking policy evaluation can read them.
// The Profile service implements this; the Customer service depends on the interface.
public interface IProfileBootstrapSink
{
    Task UpsertAsync(
        string tenantId,
        string subjectHash,
        bool isActive,
        bool parkingEligible,
        bool hasCompanyCar,
        bool accessibilityEligible,
        bool reservedSpaceEligible,
        string factSource,
        CancellationToken ct);
}
