using Dapr.Client;
using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Identity;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDaprClient();
builder.Services.AddSingleton<IParkingPolicyRepository, DaprParkingPolicyRepository>();
builder.Services.AddSingleton<IParkingSlotRepository, DaprParkingSlotRepository>();
builder.Services.AddSingleton<ISlotChangeRepository, DaprSlotChangeRepository>();
builder.Services.AddSingleton<ISeatMapRepository, DaprSeatMapRepository>();
builder.Services.AddSingleton<ISeatBlockRepository, DaprSeatBlockRepository>();
builder.Services.AddSingleton<ISeatMapChangeRepository, DaprSeatMapChangeRepository>();

builder.Services.AddScoped<IConfigurationTenantPurger, ConfigurationTenantPurger>();
builder.Services.AddScoped<ConfigurationTenantStorePurger>();

builder.Services.AddScoped<ParkingPolicyService>();
builder.Services.AddScoped<ParkingSlotService>();
builder.Services.AddScoped<SeatMapService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fairspot-configuration", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();
builder.Services.AddFpsDurableDeactivatedUserStore();

var app = builder.Build();

app.UseFpsMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed demo configuration data in Development if the tenant default policy is not yet stored.
// With a durable Dapr store the check prevents re-seeding after the first run.
if (app.Environment.IsDevelopment())
{
    await SeedDevelopmentConfigurationAsync(app);
}

app.UseFpsRequestTraceLogging();
app.MapFpsMetrics();
app.MapFpsHealthChecks();
app.Run();

static async Task SeedDevelopmentConfigurationAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DevelopmentConfigurationSeed");
    var policyRepo = app.Services.GetRequiredService<IParkingPolicyRepository>();
    var slotRepo = app.Services.GetRequiredService<IParkingSlotRepository>();
    var slotChangeRepo = app.Services.GetRequiredService<ISlotChangeRepository>();
    var demoTenantId = app.Configuration["FPS_DEMO_TENANT_ID"] ?? "demo";

    const int maxAttempts = 30;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            if (await policyRepo.GetTenantDefaultAsync(demoTenantId) is not null)
                return;

            await policyRepo.SaveAsync(new ParkingPolicy
            {
                TenantId = demoTenantId,
                TimeZone = "Europe/Prague",
                DrawCutOffTime = new TimeOnly(18, 0),
                DailyRequestCap = 100,
                AllocationLookbackDays = 10,
                LateCancellationPenalty = 1,
                NoShowPenalty = 2,
                ManualAdjustmentEnabled = true,
                SameDayBookingEnabled = true,
                SameDayUsesRequestCap = true,
                AutomaticReallocationEnabled = true,
                CompanyCarTier1Enabled = true,
                CompanyCarOverflowBehavior = "reject",
                PublishedByUserId = "seed",
                PublishedAt = DateTimeOffset.UtcNow,
                PublicationReason = "Local demo seed",
            });

            var slots = Enumerable.Range(1, 15).Select(i => new ParkingSlot
            {
                SlotId = (300 + i).ToString(),
                TenantId = demoTenantId,
                LocationId = "Prague",
                IsActive = true,
                HasCharger = i <= 3,
                IsAccessible = i == 1,
                IsCompanyCarOnly = false,
                IsMotorcycleCapacity = false,
            }).ToList();

            await slotRepo.ReplaceLocationSlotsAsync(demoTenantId, "Prague", slots);
            await slotChangeRepo.RecordAsync(new SlotChangeRecord
            {
                TenantId = demoTenantId,
                LocationId = "Prague",
                ChangedByUserId = "seed",
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeReason = "Local demo seed",
                SlotCount = slots.Count,
            });
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogInformation(
                ex,
                "Development configuration seed is waiting for Dapr state store. Attempt {Attempt}/{MaxAttempts}",
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    await policyRepo.GetTenantDefaultAsync(demoTenantId);
}

public partial class Program { }
