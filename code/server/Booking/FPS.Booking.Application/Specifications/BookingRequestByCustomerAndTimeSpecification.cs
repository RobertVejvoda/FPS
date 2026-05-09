using FPS.Booking.Application.Domain;

namespace FPS.Booking.Application.Specifications
{
    public class BookingRequestByCustomerAndTimeSpecification
    {
        private string customerId;
        private DateTime requestedDate;
        private DateTime endDateTime;

        public BookingRequestByCustomerAndTimeSpecification(string customerId, DateTime requestedDate, DateTime endDateTime)
        {
            this.customerId = customerId;
            this.requestedDate = requestedDate;
            this.endDateTime = endDateTime;
        }
    }
}