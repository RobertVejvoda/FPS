using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace FPS.Booking.Tests.API;

// SEC003 (#495): OpenAPI and Scalar docs are gated to Development so hosted
// profiles don't advertise the API surface for reconnaissance. The Booking
// service is the sample — all six gated Program.cs files use the same
// `if (app.Environment.IsDevelopment()) { MapOpenApi(); MapScalarApiReference(); }`
// pattern, so single-service end-to-end coverage is sufficient.
public sealed class SecurityDocsGatingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SecurityDocsGatingTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task OpenApiJson_NotMounted_In_NonDevelopmentEnvironment()
    {
        var prodFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));

        var client = prodFactory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ScalarUi_NotMounted_In_NonDevelopmentEnvironment()
    {
        var prodFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));

        var client = prodFactory.CreateClient();
        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiJson_IsAvailable_In_DevelopmentEnvironment()
    {
        // Local dev experience and the API client generator both rely on
        // /openapi/v1.json being available when the host is Development.
        var devFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development"));

        var client = devFactory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
