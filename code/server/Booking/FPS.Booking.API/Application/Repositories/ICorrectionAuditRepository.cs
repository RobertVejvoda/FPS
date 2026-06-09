using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface ICorrectionAuditRepository
{
    Task SaveAsync(CorrectionAuditDto audit, CancellationToken cancellationToken = default);
}
