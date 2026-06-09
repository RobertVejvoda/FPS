using FPS.Booking.Infrastructure.Services;
using FPS.SharedKernel.Profile;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FPS.Booking.Infrastructure.Tests.Services;

public sealed class HttpProfileSnapshotServiceTests
{
    private readonly Mock<IHttpContextAccessor> httpContextAccessor = new();

    [Fact]
    public async Task GetSnapshot_WithoutAuthHeader_ReturnsNull()
    {
        httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var service = new HttpProfileSnapshotService(new HttpClient(), httpContextAccessor.Object);

        var result = await service.GetSnapshotAsync("tenant-1", "user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshot_ForwardsAuthorizationHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        HttpRequestMessage? captured = null;
        var snapshot = new ProfileSnapshot("tenant-1", "user-1", "Active", true, false, false, false, [], "v1");
        var handler = new CapturingHandler(HttpStatusCode.OK, JsonSerializer.Serialize(snapshot), r => captured = r);
        var service = new HttpProfileSnapshotService(
            new HttpClient(handler) { BaseAddress = new Uri("http://fps-profile") },
            httpContextAccessor.Object);

        await service.GetSnapshotAsync("tenant-1", "user-1");

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("Authorization"));
        Assert.Contains("Bearer test-token", captured.Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task GetSnapshot_ProfileReturns401_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        var handler = new CapturingHandler(HttpStatusCode.Unauthorized, string.Empty);
        var service = new HttpProfileSnapshotService(
            new HttpClient(handler) { BaseAddress = new Uri("http://fps-profile") },
            httpContextAccessor.Object);

        var result = await service.GetSnapshotAsync("tenant-1", "user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshot_ProfileReturnsSnapshot_Deserializes()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        var expected = new ProfileSnapshot("tenant-1", "user-1", "Active", true, true, false, false,
            [new VehicleSnapshot("v-1", "ABC-123", "Sedan", false, true)], "v42");
        var handler = new CapturingHandler(HttpStatusCode.OK, JsonSerializer.Serialize(expected));
        var service = new HttpProfileSnapshotService(
            new HttpClient(handler) { BaseAddress = new Uri("http://fps-profile") },
            httpContextAccessor.Object);

        var result = await service.GetSnapshotAsync("tenant-1", "user-1");

        Assert.NotNull(result);
        Assert.Equal("v42", result!.SnapshotVersion);
        Assert.True(result.HasCompanyCar);
        Assert.Single(result.Vehicles);
    }

    private sealed class CapturingHandler(
        HttpStatusCode status,
        string body,
        Action<HttpRequestMessage>? onRequest = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest?.Invoke(request);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
