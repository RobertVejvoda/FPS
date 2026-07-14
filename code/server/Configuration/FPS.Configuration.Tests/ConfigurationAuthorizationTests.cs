using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
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
        new(Encoding.UTF8.GetBytes("fairspot-config-test-signing-key-at-least-32!!"));

    public ConfigurationAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            // PLAT001 seeded allowlist (FairSpot-controlled realm) so privileged roles
            // pass through for tenants not yet explicitly mapped — matches the demo profile.
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:TrustedRealmRoles"] = "admin,hr_manager,auditor,report_viewer",
                }));

            builder.ConfigureTestServices(services =>
            {
                // Replace Dapr-backed repositories with in-memory stubs so tests
                // run without a Dapr sidecar.
                services.AddSingleton<IParkingPolicyRepository, InMemoryParkingPolicyRepository>();
                services.AddSingleton<IParkingSlotRepository, InMemoryParkingSlotRepository>();
                services.AddSingleton<ISlotChangeRepository, InMemorySlotChangeRepository>();
                services.AddSingleton<ISeatMapRepository, InMemorySeatMapRepository>();
                services.AddSingleton<ISeatBlockRepository, InMemorySeatBlockRepository>();
                services.AddSingleton<ISeatMapChangeRepository, InMemorySeatMapChangeRepository>();
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();

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

    // SEAT001 (#783) — seat-map endpoints

    [Fact]
    public async Task GetSeatMap_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/locations/loc-1/seat-map");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSeatMap_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/seat-map");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSeatMap_HrManagerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/configuration/locations/loc-1/seat-map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PutSeatMap_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PutAsync(
            "/configuration/locations/loc-1/seat-map",
            JsonContent("""{"areas":[],"seats":[]}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutSeatMap_AdminRole_Returns204()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.PutAsync(
            "/configuration/locations/loc-1/seat-map",
            JsonContent("""{"areas":[],"seats":[]}"""));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddSeatBlock_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PostAsync(
            "/configuration/locations/loc-1/seat-blocks",
            JsonContent("""{"seatId":"S1","fromDate":"2026-08-01","toDate":"2026-08-02","reason":"Maintenance"}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployeeSeatMap_EmployeeRole_Returns200()
    {
        // The whole point of the employee-safe seat map: employees must reach it
        // for seat preference selection without any HR/admin role.
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/seat-map/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployeeSeatMap_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/locations/loc-1/seat-map/map");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /configuration/locations/{locationId}/slots/map — public-safe Parking Map (MAP001)

    [Fact]
    public async Task GetSlotsMap_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/configuration/locations/loc-1/slots/map");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSlotsMap_EmployeeRole_Returns200()
    {
        // The whole point of MAP001: employees must reach the public-safe map.
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSlotsMap_HrManagerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSlotsMap_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSlots_EmployeeRole_StillReturns403_AfterMapEndpointOpened()
    {
        // Regression guard: opening /slots/map to employees must not relax /slots,
        // which still carries the HR/admin role restriction at the action level.
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSlotsMap_Response_DoesNotContainReservedForUserId()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/configuration/locations/loc-1/slots/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reservedForUserId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReservedForUserId", body);
    }

    // Cross-service ID002 verification: TenantClaimsTransformation applies to Configuration too.

    [Fact]
    public async Task GetParkingPolicy_WithMappedIdpGroup_IsAcceptedAfterTenantRoleMapping()
    {
        // Configure tenant role mapping: idp_hr_group → hr_manager for tenant-mapped
        var mappingFactory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Test");
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantRoleMapping:tenant-mapped:idp_hr_group"] = "hr_manager",
                }));
            b.ConfigureTestServices(services =>
                services.PostConfigureAll<JwtBearerOptions>(opts =>
                {
                    opts.Authority = null;
                    opts.RequireHttpsMetadata = false;
                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero, IssuerSigningKey = TestKey,
                        RoleClaimType = ClaimTypes.Role, NameClaimType = ClaimTypes.NameIdentifier
                    };
                }));
        });

        // User token has raw IdP group "idp_hr_group" — not a native FPS role
        var client = mappingFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-x", "tenant-mapped", "idp_hr_group"));

        var response = await client.GetAsync("/configuration/parking-policy");

        // Transformation maps idp_hr_group → hr_manager, which satisfies [Authorize(Roles = "hr_manager")]
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 or 404 after role mapping, got {response.StatusCode}");
    }

    [Fact]
    public async Task PutParkingPolicy_DeactivatedUser_Returns403()
    {
        var store = factory.Services.GetRequiredService<IDeactivatedUserStore>();
        store.Deactivate("tenant-1", "deactivated-admin");

        var client = ClientWithToken("deactivated-admin", "tenant-1", "admin");
        var response = await client.PutAsync("/configuration/parking-policy", JsonContent(ValidPolicyBody()));

        // Default policy rejects users with fps_deactivated=true even when they hold the right role
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        store.Reactivate("tenant-1", "deactivated-admin");
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
