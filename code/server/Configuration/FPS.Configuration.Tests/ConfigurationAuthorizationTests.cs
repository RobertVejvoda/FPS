using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace FPS.Configuration.Tests;

public sealed class ConfigurationAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-config-test-signing-key-at-least-32!!"));

    public ConfigurationAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        IssuerSigningKey = TestKey,
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });
            });
        });
    }

    // GET /configuration/parking-policy

    [Fact]
    public async Task GetParkingPolicy_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/parking-policy");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetParkingPolicy_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/parking-policy");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetParkingPolicy_AdminRole_Returns404OrOk()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/configuration/parking-policy");
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK,
            $"Expected 404 or 200 but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetParkingPolicy_HrManagerRole_Returns404OrOk()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/configuration/parking-policy");
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK,
            $"Expected 404 or 200 but got {response.StatusCode}");
    }

    // PUT /configuration/parking-policy

    [Fact]
    public async Task PutParkingPolicy_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().PutAsync(
            "/configuration/parking-policy",
            JsonContent(ValidPolicyBody()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutParkingPolicy_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PutAsync("/configuration/parking-policy", JsonContent(ValidPolicyBody()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutParkingPolicy_AdminRole_Returns204()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.PutAsync("/configuration/parking-policy", JsonContent(ValidPolicyBody()));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PutParkingPolicy_HrManagerRole_Returns204()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.PutAsync("/configuration/parking-policy", JsonContent(ValidPolicyBody()));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // GET /configuration/locations/{locationId}/parking-policy

    [Fact]
    public async Task GetLocationPolicy_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/locations/loc-1/parking-policy");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLocationPolicy_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/parking-policy");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLocationPolicy_AdminRole_Returns404OrOk()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/configuration/locations/loc-1/parking-policy");
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK,
            $"Expected 404 or 200 but got {response.StatusCode}");
    }

    // GET /configuration/locations/{locationId}/slots

    [Fact]
    public async Task GetSlots_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/locations/loc-1/slots");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSlots_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSlots_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // PUT /configuration/locations/{locationId}/slots

    [Fact]
    public async Task PutSlots_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().PutAsync(
            "/configuration/locations/loc-1/slots",
            JsonContent("""{"slots":[]}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutSlots_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PutAsync("/configuration/locations/loc-1/slots", JsonContent("""{"slots":[]}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutSlots_AdminRole_Returns204()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.PutAsync("/configuration/locations/loc-1/slots", JsonContent("""{"slots":[]}"""));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private HttpClient ClientWithToken(string userId, string tenantId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(userId, tenantId, role));
        return client;
    }

    private static string CreateToken(string userId, string tenantId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new("tenant_id", tenantId),
            new(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static System.Net.Http.StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static string ValidPolicyBody() => """
        {
            "timeZone": "Europe/Prague",
            "drawCutOffTime": "18:00:00",
            "dailyRequestCap": 100,
            "allocationLookbackDays": 10,
            "lateCancellationPenalty": 1,
            "noShowPenalty": 2,
            "manualAdjustmentEnabled": true,
            "sameDayBookingEnabled": true,
            "sameDayUsesRequestCap": true,
            "automaticReallocationEnabled": true,
            "usageConfirmationRequired": false,
            "usageConfirmationWindowMinutes": 0,
            "usageConfirmationMethods": [],
            "noShowDetectionEnabled": false,
            "companyCarTier1Enabled": true,
            "companyCarOverflowBehavior": "reject",
            "publicationReason": null
        }
        """;
}
