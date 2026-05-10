using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprCorrectionAuditRepository : ICorrectionAuditRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprCorrectionAuditRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task SaveAsync(CorrectionAuditDto audit, CancellationToken cancellationToken = default)
    {
        // Each correction is stored with a unique key to preserve append-only semantics.
        var key = $"correction:{audit.RequestId}:{audit.AppliedAt:yyyyMMddHHmmssfff}:{audit.Id}";
        await daprClient.SaveStateAsync(BookingStore, key, audit, cancellationToken: cancellationToken);
    }
}
