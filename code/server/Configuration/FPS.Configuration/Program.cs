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

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
