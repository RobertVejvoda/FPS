using FPS.SharedKernel.Filters;

namespace FPS.DataHub.Tests;

// SEC001 (#493): mirror of the Audit ingestion-guard reflection test.
// The end-to-end HTTP behaviour is covered in Audit.Tests; here we pin
// that the attribute remains on the DataHub controller so an accidental
// removal in a future refactor surfaces immediately.
public sealed class SecurityIngestionGuardTests
{
    [Fact]
    public void DataHub_BookingEventsController_HasDaprInternalOnlyAttribute()
    {
        var attr = typeof(FPS.DataHub.Controllers.BookingEventsController)
            .GetCustomAttributes(typeof(DaprInternalOnlyAttribute), inherit: false);
        Assert.NotEmpty(attr);
    }
}
