using System;

namespace FPS.Booking.Application.Queries
{
    public class CheckAvailabilityQuery
    {
        public Guid FacilityId { get; set; }
        public string VehicleType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}