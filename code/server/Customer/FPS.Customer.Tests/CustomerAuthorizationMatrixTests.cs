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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace FPS.Customer.Tests;

// PLAT001 Layer-1 authorization matrix — role x issuer x tenant across the Customer
// admin surface, through the real JWT → claims-transformation → authorization pipeline.
//
// Auth:PlatformIssuer is configured so the issuer-gating is genuinely exercised: a
// platform_admin role is honored only on a token whose `iss` matches the platform
// issuer. The JWT layer accepts any test-signed token (ValidateIssuer=false), so the
// `iss` claim — and therefore the platform/tenant plane decision — is driven entirely
// by the claims transformation, exactly as in production.
public sealed class CustomerAuthorizationMatrixTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PlatformIssuer = "https://platform.test/realms/fps-platform";
    private const string CustomerIssuer = "https://auth.test/realms/fairspot";

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-customer-test-signing-key-at-least-32!!"));

    private readonly WebApplicationFactory<Program> factory;

    public CustomerAuthorizationMatrixTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:PlatformIssuer"] = PlatformIssuer,
                    // FairSpot-controlled realm: its admin/hr_manager/... realm roles may pass
                    // through for tenants not yet explicitly mapped (PLAT001 seeded allowlist).
                    ["Auth:TrustedRealmRoles"] = "admin,hr_manager,auditor,report_viewer",
                }));
            builder.ConfigureTestServices(services =>
            {
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
                    options.MapInboundClaims = false; // keep `iss`/`tenant_id` claim names intact
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        IssuerSigningKey = TestKey,
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier,
                    };
                });
            });
        });
    }

    // ── POST /tenants — platform-plane operation (RequirePlatformAdmin) ─────────

    [Fact]
    public async Task CreateTenant_PlatformAdmin_PassesGate()
    {
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin)
            .PostAsync("/tenants", TenantBody());
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Fact]
    public async Task CreateTenant_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).PostAsync("/tenants", TenantBody());
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_CustomerTokenWithForgedPlatformAdminClaim_IsForbidden()
    {
        // platform_admin claim minted by a customer issuer is stripped → no platform plane.
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAdmin).PostAsync("/tenants", TenantBody());
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── GET /tenants/{id}/readiness — tenant-scoped (RequireTenantAdmin) ────────

    [Fact]
    public async Task Readiness_AdminOwnTenant_PassesGate()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/tenants/acme/readiness");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Fact]
    public async Task Readiness_AdminOtherTenant_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/tenants/globex/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Readiness_PlatformAdminOtherTenant_PassesGate()
    {
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin).GetAsync("/tenants/globex/readiness");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Theory]
    [InlineData(FpsRoles.Employee)]
    [InlineData(FpsRoles.Auditor)]
    [InlineData(FpsRoles.ReportViewer)]
    [InlineData(FpsRoles.HrManager)]
    public async Task Readiness_NonAdminRoles_AreForbidden(string role)
    {
        var r = await Client(CustomerIssuer, "acme", role).GetAsync("/tenants/acme/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Readiness_CustomerTokenWithForgedPlatformAdminClaim_OtherTenant_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAdmin).GetAsync("/tenants/globex/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private HttpClient Client(string issuer, string? tenantId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(issuer, tenantId, roles));
        return client;
    }

    private static string Token(string issuer, string? tenantId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u1"), new("sub", "u1") };
        if (tenantId is not null) claims.Add(new Claim("tenant_id", tenantId));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken(
            issuer: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool PassedAuthGate(HttpStatusCode s) =>
        s != HttpStatusCode.Forbidden && s != HttpStatusCode.Unauthorized;

    private static StringContent TenantBody() => new(
        """{"displayName":"Acme","region":"eu","timeZone":"Europe/Prague","supportContacts":[]}""",
        Encoding.UTF8, "application/json");
}
