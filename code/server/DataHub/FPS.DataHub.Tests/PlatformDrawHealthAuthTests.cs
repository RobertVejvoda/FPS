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

// PLAT008E — authorization matrix + aggregate-only contract for the platform Draw-health endpoint.
// Mirrors PlatformUsageStatsAuthTests: platform_* roles are honored only on a platform-issuer token;
// a platform_* role forged on a customer-issuer token is stripped, so tenant/customer tokens fail.
public sealed class PlatformDrawHealthAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PlatformIssuer = "https://platform.test/realms/fps-platform";
    private const string CustomerIssuer = "https://auth.test/realms/fairspot";
    private const string Path = "/datahub/platform/draw-health";

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-datahub-test-signing-key-at-least-32!!"));

    private readonly WebApplicationFactory<Program> factory;

    public PlatformDrawHealthAuthTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:PlatformAuthority"] = PlatformIssuer,
                    ["Auth:PlatformIssuer"] = PlatformIssuer,
                    ["Auth:TrustedRealmRoles"] = "admin,hr_manager,auditor,report_viewer",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DataHubDbContext>>();
                services.RemoveAll<DataHubDbContext>();
                var efProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
                services.AddDbContext<DataHubDbContext>(o =>
                    o.UseInMemoryDatabase("DataHubDrawHealthAuthTest").UseInternalServiceProvider(efProvider));
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
    public async Task DrawHealth_PlatformRoles_PassGate(string role)
    {
        var r = await Client(PlatformIssuer, tenantId: null, role).GetAsync(Path);
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(PassedAuthGate(r.StatusCode), $"expected not-401/403, got {(int)r.StatusCode}: {body}");
    }

    [Fact]
    public async Task DrawHealth_TenantAdmin_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.Admin).GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task DrawHealth_CustomerTokenWithForgedPlatformRole_IsForbidden()
    {
        var r = await Client(CustomerIssuer, "acme", FpsRoles.PlatformAdmin).GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task DrawHealth_Unauthenticated_IsUnauthorized()
    {
        var r = await factory.CreateClient().GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task DrawHealth_CountsFailuresAndStuck_AggregateOnly_NoPii()
    {
        var now = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
            db.DrawHistory.AddRange(
                // A clean completed draw.
                new DrawHistoryProjection { DrawAttemptId = "d-ok", TenantId = "tenant-a", LocationId = "GL-HQ", Status = "Completed", StartedAt = now.AddHours(-2), CompletedAt = now.AddHours(-2).AddMinutes(1), LastUpdatedAt = now.AddHours(-2) },
                // A failed draw with a (private) failure reason that must NOT leak into the aggregate.
                new DrawHistoryProjection { DrawAttemptId = "d-fail", TenantId = "tenant-b", LocationId = "GL-HQ", Status = "Failed", StartedAt = now.AddHours(-1), CompletedAt = now.AddHours(-1), SafeFailureReason = "seed-only-reason", TriggeredBy = "actor-hash-xyz", LastUpdatedAt = now.AddHours(-1) },
                // A stuck draw: Running, started long ago, never completed.
                new DrawHistoryProjection { DrawAttemptId = "d-stuck", TenantId = "tenant-c", LocationId = "GL-HQ", Status = "Running", StartedAt = now.AddHours(-3), CompletedAt = null, LastUpdatedAt = now.AddHours(-3) });
            await db.SaveChangesAsync();
        }

        var r = await Client(PlatformIssuer, tenantId: null, FpsRoles.PlatformOperator).GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();

        Assert.Contains("\"failedCount\":1", body);
        Assert.Contains("\"stuckCount\":1", body);
        Assert.Contains("\"completedCount\":1", body);
        Assert.Contains("\"hasEvidence\":true", body);
        Assert.Contains("\"stale\":false", body);
        // Aggregate only — never a draw attempt id, tenant/location id, actor, or raw failure text.
        Assert.DoesNotContain("d-fail", body);
        Assert.DoesNotContain("seed-only-reason", body);
        Assert.DoesNotContain("actor-hash-xyz", body);
        Assert.DoesNotContain("tenant-b", body);
        Assert.DoesNotContain("GL-HQ", body);
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
