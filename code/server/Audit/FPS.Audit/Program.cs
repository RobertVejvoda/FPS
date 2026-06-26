using Dapr.Workflow;
using FPS.Audit.Application;
using FPS.Audit.Application.Privacy;
using FPS.Audit.Domain;
using FPS.Audit.Identity;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();

// Share the same in-memory instance for append, query, and retention interfaces.
var inMemoryAuditRepo = new InMemoryAuditRepository();
builder.Services.AddSingleton<IAuditRepository>(inMemoryAuditRepo);
builder.Services.AddSingleton<IAuditQueryRepository>(inMemoryAuditRepo);
builder.Services.AddSingleton<IAuditRetentionRepository>(inMemoryAuditRepo);
builder.Services.AddSingleton<IPiiMappingRepository, InMemoryPiiMappingRepository>();
builder.Services.AddSingleton<IErasureRequestRepository, InMemoryErasureRequestRepository>();

builder.Services.AddScoped<BookingEventAuditHandler>();
builder.Services.AddScoped<AuditQueryService>();
builder.Services.AddScoped<PiiErasureService>();
builder.Services.AddScoped<AuditRetentionService>();
builder.Services.AddScoped<AuditIntegrityService>();
builder.Services.AddScoped<IErasureWorkflowClient, DaprErasureWorkflowClient>();
builder.Services.AddScoped<PrivacyService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Dapr Workflow: erasure orchestration and service-owned activities
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<ErasureWorkflow>();
    options.RegisterActivity<CheckActiveBookingsActivity>();
    options.RegisterActivity<EraseProfileActivity>();
    options.RegisterActivity<EraseBookingDataActivity>();
    options.RegisterActivity<EraseNotificationActivity>();
    options.RegisterActivity<AnonymiseReportingActivity>();
    options.RegisterActivity<ErasePiiMappingActivity>();
    options.RegisterActivity<RecordErasureStepActivity>();
});

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fps-audit", builder.Configuration);
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
