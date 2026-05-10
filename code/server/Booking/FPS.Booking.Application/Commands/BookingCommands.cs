namespace FPS.Booking.Application.Commands;

public class ConfirmArrivalCommand
{
    public DateTime ActualArrivalTime { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
}

public class CancelReservationCommand
{
    public string Reason { get; set; } = string.Empty;
    public string CancelledBy { get; set; } = string.Empty;
}
