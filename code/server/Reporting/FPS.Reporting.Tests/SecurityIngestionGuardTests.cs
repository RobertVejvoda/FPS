using FPS.SharedKernel.Filters;

namespace FPS.Reporting.Tests;

// SEC001 (#493): mirror of the Audit ingestion-guard reflection test.
// See Audit.Tests/SecurityIngestionGuardTests for the HTTP-level coverage.
public sealed class SecurityIngestionGuardTests
{
    [Fact]
    public void Reporting_BookingEventsController_HasDaprInternalOnlyAttribute()
    {
        var attr = typeof(FPS.Reporting.Controllers.BookingEventsController)
            .GetCustomAttributes(typeof(DaprInternalOnlyAttribute), inherit: false);
        Assert.NotEmpty(attr);
    }
}
