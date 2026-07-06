using FPS.Audit.Application.Privacy;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FPS.Audit.Tests;

// SEC001 (#493): pub/sub ingestion endpoints must not accept external
// callers when the Dapr app token is configured. The shared
// DaprInternalOnly filter does the actual check; these tests pin the
// attribute is wired onto the Audit controller and end-to-end through
// the HTTP pipeline.
public sealed class SecurityIngestionGuardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TokenHeader = "dapr-api-token";
    private const string ConfigKey = "APP_API_TOKEN";
    private const string TestToken = "fairspot-test-app-token";

    private readonly WebApplicationFactory<Program> factory;

    public SecurityIngestionGuardTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(ConfigKey, TestToken);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
                var inMemAudit = new InMemoryAuditRepository();
                services.AddSingleton<IAuditRepository>(inMemAudit);
                services.AddSingleton<IAuditQueryRepository>(inMemAudit);
                services.AddSingleton<IAuditRetentionRepository>(inMemAudit);
                services.AddSingleton<IPiiMappingRepository, InMemoryPiiMappingRepository>();
                services.AddSingleton<IErasureRequestRepository, InMemoryErasureRequestRepository>();
            });
        });
    }

    [Fact]
    public void Audit_BookingEventsController_HasDaprInternalOnlyAttribute()
    {
        // Reflection-level guard: even if the WAF integration test is
        // skipped or shimmed, the source-of-truth attribute must remain
        // on the controller class. Defence against accidental removal.
        var attr = typeof(FPS.Audit.Controllers.BookingEventsController)
            .GetCustomAttributes(typeof(DaprInternalOnlyAttribute), inherit: false);
        Assert.NotEmpty(attr);
    }

    [Fact]
    public async Task PostBookingEvents_WithoutDaprToken_Returns403_WhenAppTokenConfigured()
    {
        // External caller posting straight at the ingestion path. With
        // APP_API_TOKEN set the filter must reject without ever invoking
        // the audit handler — corruption of audit evidence would follow
        // otherwise.
        var client = factory.CreateClient();
        var response = await client.PostAsync(
            "/audit/booking-events",
            new StringContent(SampleEnvelopeJson(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostBookingEvents_WithCorrectDaprToken_PassesGuard()
    {
        // The Dapr sidecar attaches dapr-api-token on forwarded calls.
        // We only assert the filter lets it past — 200 vs 400 depends on
        // downstream handler validation, which is out of scope here.
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/audit/booking-events")
        {
            Content = new StringContent(SampleEnvelopeJson(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add(TokenHeader, TestToken);

        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostBookingEvents_WithWrongDaprToken_Returns403()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/audit/booking-events")
        {
            Content = new StringContent(SampleEnvelopeJson(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add(TokenHeader, "wrong-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string SampleEnvelopeJson() =>
        """
        {
          "eventId": "evt-sec001",
          "eventType": "booking.requestSubmitted",
          "eventVersion": 1,
          "occurredAt": "2026-06-16T10:00:00Z",
          "tenantId": "tenant-1",
          "correlationId": "corr-sec001",
          "actorType": "system",
          "source": "booking",
          "payload": {}
        }
        """;

    static SecurityIngestionGuardTests()
    {
        // Reference the AuthenticationHeaderValue type so the using clause is justified
        _ = typeof(AuthenticationHeaderValue);
    }
}
