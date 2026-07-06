using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace FPS.DataHub.Tests;

// PLAT005A — authorization matrix for the platform-only usage-stats endpoint. The platform_*
// roles are honored only on a platform-issuer token; the shared TenantClaimsTransformation strips
// a platform_* role forged on a customer-issuer token, so a tenant/customer token is rejected.
public sealed class PlatformUsageStatsAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PlatformIssuer = "https://platform.test/realms/fairspot-platform";
    private const string CustomerIssuer = "https://auth.test/realms/fairspot";
    private const string Path = "/datahub/platform/usage-stats?month=2026-06";

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fairspot-datahub-test-signing-key-at-least-32!!"));

    private readonly WebApplicationFactory<Program> factory;

    public PlatformUsageStatsAuthTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test"); // not Development → skip the Postgres migrate
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Same config shape as the deployment: Auth:PlatformAuthority is what activates
                    // the multi-issuer JWT + platform-role gating (Auth:PlatformIssuer overrides the
                    // issuer if it differs from the authority). The JWKS-backed acceptance of the
                    // platform realm is ConfigureFpsJwtBearer's job (covered in SharedKernel); here
                    // we sign with a test key + ValidateIssuer=false to exercise the DataHub endpoint's
                    // role gating and the claims transformation that strips forged platform roles.
                    ["Auth:PlatformAuthority"] = PlatformIssuer,
                    ["Auth:PlatformIssuer"] = PlatformIssuer,
                    ["Auth:TrustedRealmRoles"] = "admin,hr_manager,auditor,report_viewer",
                    // Throwaway connection string so DataHub startup doesn't fail closed in the Test
                    // environment (base appsettings.json no longer ships one — SEC012A #742). The DbContext
                    // is swapped to in-memory below, so this is never used to connect.
                    ["ConnectionStrings:DataHub"] = "Host=localhost;Database=datahub_test;Username=test;Password=test",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DataHubDbContext>>();
                services.RemoveAll<DataHubDbContext>();
                // Isolate the in-memory provider's internal services so it doesn't clash with the
                // app's registered Npgsql provider ("only a single database provider" error).
                var efProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
                services.AddDbContext<DataHubDbContext>(o =>
                    o.UseInMemoryDatabase("DataHubAuthTest").UseInternalServiceProvider(efProvider));
                // The durable store is Dapr-backed; use the in-memory store so auth needs no sidecar.
                services.RemoveAll<IDeactivatedUserStore>();
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.MapInboundClaims = false;
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

    [Theory]
    [InlineData(FpsRoles.PlatformAdmin)]
    [InlineData(FpsRoles.PlatformOperator)]
    [InlineData(FpsRoles.PlatformAuditor)]
    public async Task UsageStats_PlatformRoles_PassGate(string role)
    {
        var r = await Client(PlatformIssuer, tenantId: null, role).GetAsync(Path);
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-401/403, got {(int)r.StatusCode}: {body}");
    }

    [Fact]
    public async Task UsageStats_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task UsageStats_CustomerTokenWithForgedPlatformRole_IsForbidden()
    {
        // platform_admin minted by the customer issuer is stripped → no platform plane.
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAdmin).GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task UsageStats_Unauthenticated_IsUnauthorized()
    {
        var r = await factory.CreateClient().GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task UsageStats_ReturnsAggregateOnly_WithNoPii()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
            db.TenantUsageStats.Add(new TenantUsageStatsProjection
            {
                TenantId = "tenant-a",
                PeriodMonth = new DateOnly(2026, 6, 1),
                ActiveRequestorCount = 4,
                BookingRequestCount = 9,
                AllocatedCount = 5,
            });
            await db.SaveChangesAsync();
        }

        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformAuditor)
            .GetAsync("/datahub/platform/usage-stats?month=2026-06&tenantId=tenant-a");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"bookingRequestCount\":9", body);
        Assert.Contains("\"activeRequestorCount\":4", body);
        // Aggregate only — never a requestor/employee identifier or raw payload.
        Assert.DoesNotContain("requestorId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("employee", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", body, StringComparison.OrdinalIgnoreCase);
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
}
