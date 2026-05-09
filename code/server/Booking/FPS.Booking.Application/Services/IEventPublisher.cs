using System.Threading.Tasks;

namespace FPS.Booking.Application.Services
{
    public interface IEventPublisher
    {
        Task PublishEventAsync<T>(string eventType, T eventData);
    }
}