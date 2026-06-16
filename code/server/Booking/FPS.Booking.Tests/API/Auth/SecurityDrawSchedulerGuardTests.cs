using FPS.Booking.Application.Services;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;

namespace FPS.Booking.Tests.Auth;

// SEC002 (#494): /draw-scheduler is no longer anonymously callable. The
// shared [DaprInternalOnly] resource filter rejects external traffic and
// fails closed when APP_API_TOKEN is missing outside Development.
public sealed class SecurityDrawSchedulerGuardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TokenHeader = "dapr-api-token";
    private const string ConfigKey = "APP_API_TOKEN";
    private const string TestToken = "fps-booking-test-app-token";

    private readonly WebApplicationFactory<Program> factory;

    public SecurityDrawSchedulerGuardTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(ConfigKey, TestToken);
            builder.ConfigureTestServices(services =>
            {
                // The scheduler service is the only collaborator the
                // controller touches before the auth gate has run. Stub it
                // out so the test focuses on the guard behaviour.
                var scheduler = new Mock<IDrawSchedulerService>();
                scheduler
                    .Setup(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<DrawSchedulerResult>());
                services.AddSingleton(scheduler.Object);
                services.AddSingleton(new DrawSchedulerOptions());
                services.AddSingleton<ISystemClock>(new SystemClock());
            });
        });
    }

    [Fact]
    public void DrawSchedulerController_HasDaprInternalOnlyAttribute()
    {
        // Reflection guard: if the attribute is removed in a future
        // refactor, this trips immediately even when the HTTP layer is
        // shimmed elsewhere.
        var attr = typeof(FPS.Booking.Controllers.DrawSchedulerController)
            .GetCustomAttributes(typeof(DaprInternalOnlyAttribute), inherit: false);
        Assert.NotEmpty(attr);
    }

    [Fact]
    public async Task PostScheduler_WithoutDaprToken_Returns403_WhenAppTokenConfigured()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/draw-scheduler", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostScheduler_WithCorrectDaprToken_PassesGuard()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/draw-scheduler");
        request.Headers.Add(TokenHeader, TestToken);
        var response = await client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostScheduler_WithWrongDaprToken_Returns403()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/draw-scheduler");
        request.Headers.Add(TokenHeader, "wrong-token");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostScheduler_NoAppTokenConfigured_NonDevEnvironment_Returns503()
    {
        // Fail-closed regression for the scheduler path: a hosted profile
        // that forgets to set APP_API_TOKEN must not silently accept
        // anonymous scheduler ticks. The factory below intentionally does
        // NOT call UseSetting(ConfigKey, …).
        var failClosedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
            // Wipe any inherited APP_API_TOKEN so the no-config branch fires.
            builder.UseSetting(ConfigKey, string.Empty);
        });

        var client = failClosedFactory.CreateClient();
        var response = await client.PostAsync("/draw-scheduler", content: null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
