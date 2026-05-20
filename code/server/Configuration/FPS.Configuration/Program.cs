using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Identity;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IParkingPolicyRepository, InMemoryParkingPolicyRepository>();
builder.Services.AddSingleton<IParkingSlotRepository, InMemoryParkingSlotRepository>();
builder.Services.AddSingleton<ISlotChangeRepository, InMemorySlotChangeRepository>();

builder.Services.AddScoped<ParkingPolicyService>();
builder.Services.AddScoped<ParkingSlotService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        options.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
    });

builder.Services.AddFpsAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed demo configuration data in Development so smoke tests can exercise booking scenarios.
if (app.Environment.IsDevelopment())
{
    var policyRepo = app.Services.GetRequiredService<IParkingPolicyRepository>();
    var slotRepo = app.Services.GetRequiredService<IParkingSlotRepository>();
    var slotChangeRepo = app.Services.GetRequiredService<ISlotChangeRepository>();

    if (await policyRepo.GetTenantDefaultAsync("tenant-1") is null)
    {
        await policyRepo.SaveAsync(new ParkingPolicy
        {
            TenantId = "tenant-1",
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

        var slots = Enumerable.Range(1, 10).Select(i => new ParkingSlot
        {
            SlotId = $"SLOT-{i:D2}",
            TenantId = "tenant-1",
            LocationId = "LOC-MAIN",
            IsActive = true,
            HasCharger = i <= 2,
            IsAccessible = i == 1,
            IsCompanyCarOnly = false,
            IsMotorcycleCapacity = false,
        }).ToList();

        await slotRepo.ReplaceLocationSlotsAsync("tenant-1", "LOC-MAIN", slots);
        await slotChangeRepo.RecordAsync(new SlotChangeRecord
        {
            TenantId = "tenant-1",
            LocationId = "LOC-MAIN",
            ChangedByUserId = "seed",
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Local demo seed",
            SlotCount = slots.Count,
        });
    }
}

app.Run();

public partial class Program { }
