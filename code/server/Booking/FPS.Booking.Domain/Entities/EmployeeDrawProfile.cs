using FPS.SharedKernel.Interfaces;

namespace FPS.Booking.Domain.Entities;

public class EmployeeDrawProfile : IEntity<UserId>
{
    public UserId Id { get; private set; }
    public int PriorityTier { get; private set; }
    public int ConsecutiveFailedDraws { get; private set; }
    public int TotalDrawsParticipated { get; private set; }
    public int TotalWins { get; private set; }
    public DateTime? LastDrawDate { get; private set; }

    private EmployeeDrawProfile() { }

    private EmployeeDrawProfile(UserId employeeId, int priorityTier)
    {
        Id = employeeId;
        PriorityTier = priorityTier;
    }

    public static EmployeeDrawProfile Create(UserId employeeId, int priorityTier = 1)
    {
        if (employeeId is null)
            throw new BookingException("EmployeeId is required");
        if (priorityTier < 1 || priorityTier > 5)
            throw new BookingException("PriorityTier must be between 1 and 5");

        return new EmployeeDrawProfile(employeeId, priorityTier);
    }

    public int CalculateWeight(int maxFailedDrawCap)
        => PriorityTier * (1 + Math.Min(ConsecutiveFailedDraws, maxFailedDrawCap));

    public bool IsGuaranteedWin(int guaranteedWinThreshold)
        => ConsecutiveFailedDraws >= guaranteedWinThreshold;

    public void RecordWin(DateTime drawDate)
    {
        ConsecutiveFailedDraws = 0;
        TotalWins++;
        TotalDrawsParticipated++;
        LastDrawDate = drawDate;
    }

    public void RecordLoss(DateTime drawDate)
    {
        ConsecutiveFailedDraws++;
        TotalDrawsParticipated++;
        LastDrawDate = drawDate;
    }

    public void SetPriorityTier(int priorityTier)
    {
        if (priorityTier < 1 || priorityTier > 5)
            throw new BookingException("PriorityTier must be between 1 and 5");

        PriorityTier = priorityTier;
    }
}
