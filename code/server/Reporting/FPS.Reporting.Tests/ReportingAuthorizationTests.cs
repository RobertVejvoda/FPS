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

namespace FPS.Reporting.Tests;

public sealed class ReportingAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-reporting-test-signing-key-at-least-32!!"));

    public ReportingAuthorizationTests(WebApplicationFactory<Program> factory)
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

    // GET /reports/parking/summary

    [Fact]
    public async Task GetSummary_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/reports/parking/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/reports/parking/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_HrManagerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/reports/parking/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/reports/parking/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_ReportViewerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "report_viewer");
        var response = await client.GetAsync("/reports/parking/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // GET /reports/parking/fairness

    [Fact]
    public async Task GetFairness_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/reports/parking/fairness");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFairness_EmployeeRole_Returns403()
    {
        var client = ClientWithToken("user-1", "tenant-1", "employee");
        var response = await client.GetAsync("/reports/parking/fairness");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFairness_HrManagerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "hr_manager");
        var response = await client.GetAsync("/reports/parking/fairness");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetFairness_AdminRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "admin");
        var response = await client.GetAsync("/reports/parking/fairness");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetFairness_ReportViewerRole_Returns200()
    {
        var client = ClientWithToken("user-1", "tenant-1", "report_viewer");
        var response = await client.GetAsync("/reports/parking/fairness");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
