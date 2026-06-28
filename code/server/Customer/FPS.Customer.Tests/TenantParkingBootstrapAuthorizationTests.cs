using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FPS.Customer.Application;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace FPS.Customer.Tests;

// Auth-pipeline tests for TenantParkingBootstrapController. Issue #477
// (PR #486 review) needed the read-only GET to be reachable by hr_manager
// so the Configuration page's location-discovery call doesn't 403 for HR;
// mutating POSTs stay admin-only because they record tenant-wide bootstrap
// state. These tests go through the real JWT/role middleware so the split
// is pinned end-to-end.
public sealed class TenantParkingBootstrapAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-customer-test-signing-key-at-least-32!!"));

    public TenantParkingBootstrapAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                // Swap Dapr-backed repos for the in-memory equivalents so the
                // factory boot path (HydrateIdentityStores) doesn't need a
                // running Dapr sidecar. The auth gate runs the same way; only
                // the storage backend is replaced.
                services.RemoveAll<ITenantRepository>();
                services.RemoveAll<ITenantIdentityRepository>();
                services.RemoveAll<ITenantParkingBootstrapRepository>();
                services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
                services.AddSingleton<ITenantIdentityRepository, InMemoryTenantIdentityRepository>();
                services.AddSingleton<ITenantParkingBootstrapRepository, InMemoryTenantParkingBootstrapRepository>();
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

    // ── GET /tenants/{tenantId}/parking-bootstrap (discovery) ─────────────────

    [Fact]
    public async Task GetBootstrap_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/tenants/demo/parking-bootstrap");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "demo", "employee");
        var response = await client.GetAsync("/tenants/demo/parking-bootstrap");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_HrManagerRole_PassesAuthGate()
    {
        // The Codex finding: HR opens the Configuration page, which calls
        // this endpoint to discover known locations. Must not 403. (The
        // downstream Dapr state call may fail in the test harness — that's
        // out of scope here; we only assert the auth gate let us through.)
        var client = ClientWithToken("user-1", "demo", "hr_manager");
        var response = await client.GetAsync("/tenants/demo/parking-bootstrap");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_AdminRole_PassesAuthGate()
    {
        var client = ClientWithToken("user-1", "demo", "admin");
        var response = await client.GetAsync("/tenants/demo/parking-bootstrap");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_HrManagerOtherTenant_Returns403()
    {
        // Codex re-review on PR #486: opening the GET to hr_manager must
        // not let an HR user read another tenant's bootstrap data. Token
        // carries tenant_id=tenant-a; request goes to /tenants/tenant-b/...
        var client = ClientWithToken("user-1", "tenant-a", "hr_manager");
        var response = await client.GetAsync("/tenants/tenant-b/parking-bootstrap");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_AdminOtherTenant_IsForbidden()
    {
        // PLAT001: admin is now tenant-scoped. A tenant-a admin cannot read
        // tenant-b's bootstrap — cross-tenant access requires platform_admin.
        var client = ClientWithToken("user-1", "tenant-a", "admin");
        var response = await client.GetAsync("/tenants/tenant-b/parking-bootstrap");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBootstrap_CustomerTokenWithPlatformAdminClaim_IsForbiddenCrossTenant()
    {
        // A customer-issuer token can never reach the platform plane: even if its
        // IdP injects a platform_admin claim, the claims transformation strips it,
        // so it cannot cross tenants.
        var client = ClientWithToken("user-1", "tenant-a", "platform_admin");
        var response = await client.GetAsync("/tenants/tenant-b/parking-bootstrap");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /tenants/{tenantId}/parking-bootstrap/policy (mutating) ──────────

    [Fact]
    public async Task PostPolicy_HrManagerRole_Returns403_StillAdminOnly()
    {
        // Privacy/scope boundary: opening the GET to hr_manager must not
        // accidentally relax the mutating endpoints.
        var client = ClientWithToken("user-1", "demo", "hr_manager");
        var response = await client.PostAsync(
            "/tenants/demo/parking-bootstrap/policy",
            JsonBody("""{"timeZone":"Europe/Prague","drawCutOffTime":"18:00:00","dailyRequestCap":100,"allocationLookbackDays":10}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostPolicy_AdminRole_DoesNotReturn403()
    {
        // 400 (validation) or 204 are acceptable here — the point is the
        // admin role makes it past the auth gate.
        var client = ClientWithToken("user-1", "demo", "admin");
        var response = await client.PostAsync(
            "/tenants/demo/parking-bootstrap/policy",
            JsonBody("""{"timeZone":"Europe/Prague","drawCutOffTime":"18:00:00","dailyRequestCap":100,"allocationLookbackDays":10}"""));
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /tenants/{tenantId}/parking-bootstrap/locations (mutating) ───────

    [Fact]
    public async Task PostLocation_HrManagerRole_Returns403()
    {
        var client = ClientWithToken("user-1", "demo", "hr_manager");
        var response = await client.PostAsync(
            "/tenants/demo/parking-bootstrap/locations",
            JsonBody("""{"locationId":"Prague","activeSlotCount":15,"hasLocationPolicy":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, "application/json");
}
