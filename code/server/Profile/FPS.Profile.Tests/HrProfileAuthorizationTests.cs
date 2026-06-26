using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FPS.Profile.Application;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FPS.Profile.Tests;

// Auth-pipeline integration tests for HrProfileController. Issue #474 needs
// report_viewer to be able to resolve display names so the Reports page can
// show employee names instead of opaque hashes; the requestor-summary
// endpoint stays HR/admin because it carries parking-eligibility facts that
// report_viewer is not allowed to see. These tests go through the real JWT
// + role pipeline (not direct controller method invocation), so the role
// guards are pinned end-to-end.
public sealed class HrProfileAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-profile-test-signing-key-at-least-32!!"));

    public HrProfileAuthorizationTests(WebApplicationFactory<Program> factory)
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

                // Override Dapr-backed repos with in-memory stubs so tests run without a Dapr sidecar.
                services.AddSingleton<IProfileRepository, InMemoryProfileRepository>();
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
            });
        });
    }

    // ── POST /profile/hr/display-names ───────────────────────────────────────

    [Fact]
    public async Task DisplayNames_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient()
            .PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNames_EmployeeRole_Returns403()
    {
        // The display-names endpoint must remain off-limits to plain employees.
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNames_ReportViewerRole_Returns200()
    {
        // Regression test for the Codex review on PR #475: report_viewer must
        // be able to resolve display names so the Reports page surfaces
        // employee names instead of falling back to the short ref.
        var client = ClientWithToken("user-1", "tenant-1", "report_viewer");
        var response = await client.PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNames_HrManagerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNames_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNames_AuditorRole_Returns200()
    {
        // Regression test for Codex review on PR #488 (#482): auditor must be
        // able to resolve display names so the auditor workspace can render
        // actor names instead of silently degrading to the short-ref fallback.
        var client = ClientWithToken("user-1", "tenant-1", "auditor");
        var response = await client.PostAsync("/profile/hr/display-names", JsonBody("""{"userIds":["u1"]}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GET /profile/hr/requestors/{userId} ──────────────────────────────────

    [Fact]
    public async Task RequestorSummary_ReportViewerRole_Returns403_StillHrAdminOnly()
    {
        // Privacy boundary: report_viewer can see names, but the requestor
        // summary carries parking eligibility, vehicle facts and home location
        // that are HR/admin-only. Splitting the controller-level guard must
        // not relax this endpoint.
        var client = ClientWithToken("user-1", "tenant-1", "report_viewer");
        var response = await client.GetAsync("/profile/hr/requestors/some-user");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequestorSummary_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/profile/hr/requestors/some-user");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequestorSummary_AuditorRole_Returns403_StillHrAdminOnly()
    {
        // Privacy boundary (issue #482): opening display-names to auditor
        // must not also expose parking eligibility / vehicle / home-location
        // facts on the requestor summary — that stays HR/admin-only.
        var client = ClientWithToken("user-1", "tenant-1", "auditor");
        var response = await client.GetAsync("/profile/hr/requestors/some-user");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequestorSummary_HrManagerRole_DoesNotReturn403()
    {
        // 404 (no seeded profile) is acceptable — the only thing this test
        // pins is that hr_manager makes it past the role check.
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/profile/hr/requestors/some-user");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── PATCH /profile/hr/requestors/{userId}/eligibility (issue #481) ───────

    [Fact]
    public async Task UpdateEligibility_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().PatchAsync(
            "/profile/hr/requestors/some-user/eligibility",
            JsonBody("""{"hasCompanyCar":true}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEligibility_EmployeeRole_Returns403_NoSelfService()
    {
        // The core acceptance criterion: an employee must not be able to
        // self-enable company car or accessibility eligibility, even by
        // calling the HR endpoint directly.
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.PatchAsync(
            "/profile/hr/requestors/user-1/eligibility",
            JsonBody("""{"hasCompanyCar":true,"accessibilityEligible":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEligibility_AuditorRole_Returns403_StillHrAdminOnly()
    {
        var client = ClientWithToken("user-1", "tenant-1", "auditor");
        var response = await client.PatchAsync(
            "/profile/hr/requestors/some-user/eligibility",
            JsonBody("""{"hasCompanyCar":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEligibility_ReportViewerRole_Returns403_StillHrAdminOnly()
    {
        var client = ClientWithToken("user-1", "tenant-1", "report_viewer");
        var response = await client.PatchAsync(
            "/profile/hr/requestors/some-user/eligibility",
            JsonBody("""{"hasCompanyCar":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEligibility_HrManagerRole_PassesAuthGate()
    {
        // 404 (no seeded profile) is acceptable — the auth gate is the
        // contract under test here. End-to-end behaviour is covered by
        // EmployeeBootstrapServiceTests.UpdateEligibility_*.
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.PatchAsync(
            "/profile/hr/requestors/some-user/eligibility",
            JsonBody("""{"hasCompanyCar":true}"""));
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEligibility_AdminRole_PassesAuthGate()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.PatchAsync(
            "/profile/hr/requestors/some-user/eligibility",
            JsonBody("""{"hasCompanyCar":true}"""));
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
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
