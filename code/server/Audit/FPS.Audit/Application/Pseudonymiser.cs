using System.Security.Cryptography;
using System.Text;

namespace FPS.Audit.Application;

public static class Pseudonymiser
{
    public static string? Hash(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static object SanitisePayload(BookingEventPayload payload) => new
    {
        bookingRequestId = payload.BookingRequestId,
        requestorHash = Hash(payload.RequestorId),
        locationId = payload.LocationId,
        date = payload.Date,
        timeSlot = payload.TimeSlot,
        previousStatus = payload.PreviousStatus,
        newStatus = payload.NewStatus,
        reasonCode = payload.ReasonCode,
        reasonText = payload.ReasonText,
        affectedRecipientHashes = payload.AffectedRecipientIds?.Select(Hash).ToList()
    };
}
