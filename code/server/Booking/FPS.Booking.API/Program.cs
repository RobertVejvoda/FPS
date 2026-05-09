using Dapr.Client;
using FPS.Booking.API.Workflows;
using FPS.Booking.Infrastructure;
using Microsoft.OpenApi.Models;
using NJsonSchema;
using NSwag.AspNetCore;
using NSwag.Generation.Processors.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers().AddDapr();
builder.Services.AddEndpointsApiExplorer();

// Configure OpenAPI with NSwag instead of Swagger
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "FPS Booking API";
    config.Version = "v1";
    config.Description = "API for managing parking slot bookings and allocations";
    
    // Add JWT bearer token authentication
    config.AddSecurity("JWT", Enumerable.Empty<string>(), new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Type into the textbox: Bearer {your JWT token}"
    });
    
    config.OperationProcessors.Add(
        new AspNetCoreOperationSecurityScopeProcessor("JWT"));
        
    // Add document processor for updating schema IDs
    config.DocumentProcessors.Add(new NSwag.Generation.Processors.DocumentProcessor(
        (document, _) =>
        {
            foreach (var schema in document.Components.Schemas)
            {
                if (schema.Key.StartsWith("FPS.Booking."))
                {
                    // Simplify schema names by removing namespace
                    var newKey = schema.Key.Split('.').Last();
                    document.Components.Schemas[newKey] = schema.Value;
                    document.Components.Schemas.Remove(schema.Key);
                }
            }
            return true;
        }));
});

// Register DI for infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Register Dapr Workflows
builder.RegisterWorkflows();

// Add Dapr Client
builder.Services.AddSingleton<DaprClient>(_ => new DaprClientBuilder().Build());

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDaprHealthCheck(
        name: "dapr-sidecar",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "dapr", "sidecar" },
        configureOptions: options => 
        {
            options.TimeoutSeconds = 3;
            options.CheckComponents = true;
            options.ComponentsToCheck = new List<string> { "statestore", "pubsub" };
        });

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    // Use NSwag UI instead of Swagger UI
    app.UseOpenApi();
    app.UseSwaggerUi3(config =>
    {
        config.DocumentPath = "/swagger/v1/swagger.json";
        config.Path = "/api";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();

app.Run();
