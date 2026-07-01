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
                services.RemoveAll<ITenantRequestRepository>();
                services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
                services.AddSingleton<ITenantIdentityRepository, InMemoryTenantIdentityRepository>();
                services.AddSingleton<ITenantParkingBootstrapRepository, InMemoryTenantParkingBootstrapRepository>();
                services.AddSingleton<ITenantRequestRepository, InMemoryTenantRequestRepository>();
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
                // PLAT003B — in-memory evidence store so the GET reset-sandbox evidence endpoint returns a
                // clean 404 (no recorded evidence) instead of reaching for a Dapr sidecar in the auth harness.
                services.RemoveAll<ISandboxResetEvidenceStore>();
                services.AddSingleton<ISandboxResetEvidenceStore, InMemorySandboxResetEvidenceStore>();
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

    // ── GET /tenant-requests — platform-operator triage queue (RequirePlatformOperator) ──
    // PLAT004: operators triage, admins are a superset, and a tenant admin must never reach it
    // (the queue holds cross-tenant prospect PII).

    [Fact]
    public async Task TenantRequestQueue_PlatformOperator_PassesGate()
    {
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformOperator).GetAsync("/tenant-requests");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Fact]
    public async Task TenantRequestQueue_PlatformAdmin_PassesGate()
    {
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin).GetAsync("/tenant-requests");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Fact]
    public async Task TenantRequestQueue_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/tenant-requests");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task TenantRequestQueue_PlatformAuditor_IsForbidden()
    {
        // Auditor is read-only platform staff, not an operator — cannot reach the triage queue.
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAuditor).GetAsync("/tenant-requests");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
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

    // ── GET /platform/tenants(+/{id}) — read-only platform directory/detail (RequirePlatformReader) ──
    // PLAT008B: platform_admin / operator / auditor may read; a tenant/customer token never can,
    // and authorization never uses tenant claims.

    [Theory]
    [InlineData(FpsRoles.PlatformAdmin)]
    [InlineData(FpsRoles.PlatformOperator)]
    [InlineData(FpsRoles.PlatformAuditor)]
    public async Task PlatformTenantDirectory_PlatformRoles_PassGate(string role)
    {
        var r = await Client(PlatformIssuer, tenantId: null, role).GetAsync("/platform/tenants");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Theory]
    [InlineData(FpsRoles.PlatformAdmin)]
    [InlineData(FpsRoles.PlatformOperator)]
    [InlineData(FpsRoles.PlatformAuditor)]
    public async Task PlatformTenantDetail_PlatformRoles_PassGate(string role)
    {
        // Unknown tenant → 404, which still means the auth gate was passed (not 401/403).
        var r = await Client(PlatformIssuer, tenantId: null, role).GetAsync("/platform/tenants/globex");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-403/401, got {r.StatusCode}");
    }

    [Fact]
    public async Task PlatformTenantDirectory_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/platform/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantDetail_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/platform/tenants/acme");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantDirectory_CustomerTokenWithForgedPlatformAuditorClaim_IsForbidden()
    {
        // A platform_auditor role minted by the customer issuer is stripped → no platform plane.
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAuditor).GetAsync("/platform/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantDirectory_Unauthenticated_IsUnauthorized()
    {
        var r = await factory.CreateClient().GetAsync("/platform/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantDirectory_ListsCreatedTenant()
    {
        // End-to-end: a platform_admin creates a tenant, then it appears in the directory list
        // (exercises the enumeration index + ListAsync through the platform read endpoint).
        var admin = Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAdmin);
        var create = await admin.PostAsync("/tenants",
            new StringContent("""{"slug":"globex","displayName":"Globex","region":"eu","timeZone":"Europe/Prague","supportContacts":[]}""",
                Encoding.UTF8, "application/json"));
        Assert.True(PassedAuthGate(create.StatusCode), $"create gate, got {create.StatusCode}");

        var list = await admin.GetAsync("/platform/tenants");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("globex", body);
    }

    // ── POST /platform/tenants/{id}/reset-sandbox — platform-operator sandbox reset (PLAT003A) ──
    // Operators/admins may reset the evaluation sandbox; auditor is read-only and tenant/customer
    // tokens (incl. a forged platform role) can never reach it. An unknown tenant → 404, which
    // still means the auth gate was passed (not 401/403).

    [Theory]
    [InlineData(FpsRoles.PlatformAdmin)]
    [InlineData(FpsRoles.PlatformOperator)]
    public async Task SandboxReset_PlatformOperatorRoles_PassGate(string role)
    {
        var r = await Client(PlatformIssuer, tenantId: null, role).PostAsync("/platform/tenants/greenlogistics/reset-sandbox", null);
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-401/403, got {r.StatusCode}");
    }

    [Fact]
    public async Task SandboxReset_PlatformAuditor_IsForbidden()
    {
        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAuditor).PostAsync("/platform/tenants/greenlogistics/reset-sandbox", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task SandboxReset_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).PostAsync("/platform/tenants/acme/reset-sandbox", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task SandboxReset_CustomerTokenWithForgedPlatformRole_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformOperator).PostAsync("/platform/tenants/acme/reset-sandbox", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── GET /platform/tenants/{id}/reset-sandbox — last-reset evidence (RequirePlatformReader, PLAT003B) ──
    // Read-only surface: any platform reader (auditor included) passes; tenant/customer tokens (incl. a
    // forged platform role) are refused. With no recorded evidence the gate-passing result is a 404.

    [Theory]
    [InlineData(FpsRoles.PlatformAdmin)]
    [InlineData(FpsRoles.PlatformOperator)]
    [InlineData(FpsRoles.PlatformAuditor)]
    public async Task SandboxResetEvidence_PlatformReaderRoles_PassGate(string role)
    {
        var r = await Client(PlatformIssuer, tenantId: null, role).GetAsync("/platform/tenants/greenlogistics/reset-sandbox");
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-401/403, got {r.StatusCode}");
    }

    [Fact]
    public async Task SandboxResetEvidence_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync("/platform/tenants/acme/reset-sandbox");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task SandboxResetEvidence_CustomerTokenWithForgedPlatformRole_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAuditor).GetAsync("/platform/tenants/acme/reset-sandbox");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private sealed class InMemorySandboxResetEvidenceStore : ISandboxResetEvidenceStore
    {
        public Task RecordAsync(SandboxResetEvidence e, CancellationToken ct) => Task.CompletedTask;
        public Task<SandboxResetEvidence?> GetLatestAsync(string tenantId, CancellationToken ct) => Task.FromResult<SandboxResetEvidence?>(null);
    }

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
