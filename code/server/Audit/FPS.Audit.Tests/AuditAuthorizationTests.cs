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

namespace FPS.Audit.Tests;

public sealed class AuditAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-audit-test-signing-key-at-least-32!!"));

    public AuditAuthorizationTests(WebApplicationFactory<Program> factory)
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

    // GET /audit

    [Fact]
    public async Task GetAudit_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/audit");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_AuditorRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "auditor");
        var response = await client.GetAsync("/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // DELETE /audit/pii-mappings/{userId}

    [Fact]
    public async Task DeletePiiMapping_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().DeleteAsync("/audit/pii-mappings/user-1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletePiiMapping_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.DeleteAsync("/audit/pii-mappings/user-1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeletePiiMapping_AuditorRole_Returns204()
    {
        var client = ClientWithToken("user-1", "tenant-1", "auditor");
        var response = await client.DeleteAsync("/audit/pii-mappings/user-1");
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
}
