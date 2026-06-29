using FPS.SharedKernel.Identity;
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
using System.Text.Json;

namespace FPS.Identity.Tests.Controllers;

public sealed class MeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-test-signing-key-at-least-32-chars!"));

    private const string PlatformIssuer = "https://platform.test/realms/fps-platform";

    public MeControllerTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            // PLAT001 seeded allowlist: the FairSpot-controlled realm's privileged roles may
            // pass through for tenants not yet explicitly mapped (matches the demo profile).
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:TrustedRealmRoles"] = "admin,hr_manager,auditor,report_viewer",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
                services.PostConfigureAll<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(options =>
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

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsUserContext()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "tenant-1", "employee"));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("user-1", json.GetProperty("userId").GetString());
        Assert.Equal("tenant-1", json.GetProperty("tenantId").GetString());
        Assert.Contains("employee", json.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task GetMe_WithMultipleRoles_ReturnsAllRoles()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-2", "tenant-1", "employee", "hr_manager"));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToList();
        Assert.Contains("employee", roles);
        Assert.Contains("hr_manager", roles);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithExpiredToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "tenant-1", expires: DateTime.UtcNow.AddMinutes(-1)));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_PlatformToken_NoTenant_ReturnsPlatformRoles()
    {
        // PLAT008A — a platform-issuer token is cross-tenant: platform_* roles, no tenant_id.
        // The platform issuer is configured so TenantClaimsTransformation runs TransformPlatform,
        // which keeps the platform role and marks fps_platform. /me must admit it (200) so the
        // operator console can read its roles.
        var platformFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:PlatformIssuer"] = PlatformIssuer,
                })));

        var client = platformFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreatePlatformToken("operator-1", "platform_admin"));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("operator-1", json.GetProperty("userId").GetString());
        Assert.Equal(string.Empty, json.GetProperty("tenantId").GetString());
        Assert.Contains("platform_admin", json.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task GetMe_WithMissingTenantId_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTokenWithoutClaim("user-1", omitTenantId: true));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithMissingUserId_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTokenWithoutClaim(tenantId: "tenant-1", omitUserId: true));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_CannotSpoofTenantViaQueryString_TenantComesFromToken()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "real-tenant"));

        var response = await client.GetAsync("/me?tenantId=spoofed-tenant");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("real-tenant", json.GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task GetMe_DeactivatedUser_Returns403()
    {
        // A deactivated user has a valid JWT (authenticated) but is denied by the default
        // authorization policy (forbidden). 403 is correct; 401 would imply "not authenticated".
        var store = factory.Services.GetRequiredService<FPS.SharedKernel.Identity.IDeactivatedUserStore>();
        store.Deactivate("tenant-deactivated", "deactivated-user");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("deactivated-user", "tenant-deactivated"));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        store.Reactivate("tenant-deactivated", "deactivated-user");
    }

    [Fact]
    public async Task GetMe_ReactivatedUser_Returns200()
    {
        var store = factory.Services.GetRequiredService<FPS.SharedKernel.Identity.IDeactivatedUserStore>();
        store.Deactivate("tenant-reactivate", "user-to-reactivate");
        store.Reactivate("tenant-reactivate", "user-to-reactivate");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-to-reactivate", "tenant-reactivate"));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateToken(string userId, string tenantId, params string[] roles)
        => CreateToken(userId, tenantId, DateTime.UtcNow.AddHours(1), roles);

    private static string CreateToken(string userId, string tenantId, DateTime expires, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new("tenant_id", tenantId)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // PLAT008A — a platform-issuer token: cross-tenant platform_* roles, an iss that matches
    // the configured Auth:PlatformIssuer, and deliberately no tenant_id.
    private static string CreatePlatformToken(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: PlatformIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateTokenWithoutClaim(string? userId = null, string? tenantId = null,
        bool omitUserId = false, bool omitTenantId = false)
    {
        var claims = new List<Claim>();
        if (!omitUserId && userId is not null)
        {
            claims.Add(new(ClaimTypes.NameIdentifier, userId));
            claims.Add(new("sub", userId));
        }
        if (!omitTenantId && tenantId is not null)
            claims.Add(new("tenant_id", tenantId));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
