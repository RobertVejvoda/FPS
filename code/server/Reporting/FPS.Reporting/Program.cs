using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.Reporting.Identity;
using FPS.Reporting.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
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
builder.Services.AddScoped<ReportingTenantStorePurger>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fps-reporting", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();
builder.Services.AddFpsDurableDeactivatedUserStore();

var app = builder.Build();

app.UseFpsMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.UseFpsRequestTraceLogging();
app.MapFpsMetrics();
app.MapFpsHealthChecks();
app.Run();

public partial class Program { }
