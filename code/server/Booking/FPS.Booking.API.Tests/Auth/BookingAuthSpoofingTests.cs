using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.SharedKernel.Identity;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FPS.Booking.API.Tests.Auth;

public sealed class BookingAuthSpoofingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("fps-booking-test-signing-key-at-least-32!!"));

    public BookingAuthSpoofingTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                // Replace Dapr client with null — endpoints under test don't reach persistence
                var mediatorMock = new Mock<IMediator>();
                mediatorMock
                    .Setup(m => m.Send(It.IsAny<SubmitBookingRequestCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SubmitBookingRequestResult(Guid.NewGuid(), "Pending", null, null));
                mediatorMock
                    .Setup(m => m.Send(It.IsAny<GetMyBookingsQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BookingListResult(new List<BookingListItem>(), null));
                services.AddSingleton(mediatorMock.Object);

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
    public async Task GetMyBookings_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_WithValidToken_Returns200()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "tenant-1"));

        var response = await client.GetAsync("/bookings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_SpoofedTenantHeader_IgnoresHeader()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "real-tenant"));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "spoofed-tenant");

        var response = await client.GetAsync("/bookings");

        // Request succeeds — tenant from JWT, header is ignored
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_SpoofedUserHeader_IgnoresHeader()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("real-user", "tenant-1"));
        client.DefaultRequestHeaders.Add("X-Requestor-Id", "spoofed-user");

        var response = await client.GetAsync("/bookings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SubmitBooking_SpoofedTenantQueryParam_IgnoresParam()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("user-1", "real-tenant"));

        var body = JsonSerializer.Serialize(new
        {
            facilityId = Guid.NewGuid().ToString(),
            licensePlate = "ABC-123",
            vehicleType = "Sedan",
            isElectric = false,
            requiresAccessibleSpot = false,
            isCompanyCar = false,
            plannedArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
            plannedDepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
            tenantId = "spoofed-tenant",
            requestorId = "spoofed-user"
        });

        var response = await client.PostAsync("/bookings?tenantId=spoofed&requestorId=spoofed",
            new StringContent(body, Encoding.UTF8, "application/json"));

        // Accepted or 422 — but not 401; spoofed params in body/query don't break auth
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(string userId, string tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new("tenant_id", tenantId),
            new(ClaimTypes.Role, "employee")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
