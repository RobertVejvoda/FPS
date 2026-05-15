using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.Reporting.Identity;
using FPS.Reporting.Infrastructure;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();

var inMemoryRepo = new InMemoryReportingRepository();
builder.Services.AddSingleton<IReportingRepository>(inMemoryRepo);
builder.Services.AddSingleton<IReportingQueryRepository>(inMemoryRepo);

builder.Services.AddScoped<BookingEventReportingHandler>();
builder.Services.AddScoped<ReportingQueryService>();
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
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.Run();

public partial class Program { }
