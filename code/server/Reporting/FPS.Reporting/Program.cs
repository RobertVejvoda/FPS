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

// #763 — report data comes from DataHub's durable Postgres projections so it survives a Reporting
// restart. Production/NAS: reads via a token-forwarding DataHub client, and the legacy booking-events
// projection writes to a no-op (no in-memory tenant state). Development/Test keep the in-memory
// projection so the local event→query round-trip and the unit tests still work.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test"))
{
    var inMemoryRepo = new InMemoryReportingRepository();
    builder.Services.AddSingleton<IReportingRepository>(inMemoryRepo);
    builder.Services.AddSingleton<IReportingQueryRepository>(inMemoryRepo);
}
else
{
    builder.Services.AddSingleton<IReportingRepository, NoOpReportingRepository>();
    builder.Services.AddHttpClient<IReportingQueryRepository, DataHubReportingQueryRepository>(client =>
        client.BaseAddress = new Uri(builder.Configuration["DataHubService:BaseUrl"] ?? "http://fps-datahub"));
}

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
