using FPS.SharedKernel.Profile;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace FPS.Booking.Infrastructure.Services;

public sealed class HttpProfileSnapshotService : IProfileSnapshotService
{
    private readonly HttpClient httpClient;
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpProfileSnapshotService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        this.httpClient = httpClient;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<ProfileSnapshot?> GetSnapshotAsync(
        string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "profile/snapshot");
        request.Headers.Add("Authorization", authHeader);

        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<ProfileSnapshot>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
