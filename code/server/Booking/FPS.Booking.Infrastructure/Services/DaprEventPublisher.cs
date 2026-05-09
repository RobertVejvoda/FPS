using System.Threading.Tasks;
using Dapr.Client;
using FPS.Booking.Application.Services;

namespace FPS.Booking.Infrastructure.Services
{
    public class DaprEventPublisher : IEventPublisher
    {
        private readonly DaprClient _daprClient;
        private const string PUBSUB_NAME = "booking-pubsub";

        public DaprEventPublisher(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        public async Task PublishEventAsync<T>(string eventType, T eventData)
        {
            await _daprClient.PublishEventAsync(PUBSUB_NAME, eventType, eventData);
        }
    }
}