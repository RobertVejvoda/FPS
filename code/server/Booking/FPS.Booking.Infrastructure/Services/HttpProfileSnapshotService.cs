using FPS.SharedKernel.Profile;
using System.Net.Http.Json;

namespace FPS.Booking.Infrastructure.Services;

public sealed class HttpProfileSnapshotService : IProfileSnapshotService
{
    private readonly HttpClient httpClient;

    public HttpProfileSnapshotService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    public async Task<ProfileSnapshot?> GetSnapshotAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ProfileSnapshot>(
                "profile/snapshot", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
