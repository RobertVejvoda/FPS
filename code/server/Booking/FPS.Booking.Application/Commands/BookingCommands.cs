using System;

namespace FPS.Booking.Application.Commands
{
    public class SubmitBookingRequestCommand
    {
        public Guid VehicleId { get; set; }
        public Guid FacilityId { get; set; }
        public DateTime PlannedArrivalTime { get; set; }
        public DateTime PlannedDepartureTime { get; set; }
        public string RequestedBy { get; set; }
    }

    public class ConfirmArrivalCommand
    {
        public DateTime ActualArrivalTime { get; set; }
        public string ConfirmedBy { get; set; }
    }

    public class CancelReservationCommand
    {
        public string Reason { get; set; }
        public string CancelledBy { get; set; }
    }
}