using Dapr.Client;
using Dapr.Workflow;
using FPS.Booking.API.Identity;
using FPS.Booking.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
using FPS.SharedKernel.Identity;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<FPS.Booking.Application.Workflows.DrawWorkflow>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.ResolveDrawInputActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.AcquireDrawAttemptActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.CloseRequestWindowActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.LoadPendingRequestsActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.LoadCapacityActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.LoadMetricsActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.RunAllocationActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.PersistDecisionsActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.QueueIntegrationEventsActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.CompleteDrawAttemptActivity>();
    options.RegisterActivity<FPS.Booking.Application.Workflows.Activities.FailDrawAttemptActivity>();
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "Booking API", Version = "v1" };
        doc.Servers = null;
        var components = doc.Components ?? new OpenApiComponents();
        var schemes = components.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>();
        schemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Bearer — tenant and user identity come from token claims, not request parameters."
        };
        components.SecuritySchemes = schemes;
        doc.Components = components;
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((op, ctx, _) =>
    {
        op.Security ??= [];
        op.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", ctx.Document)] = []
        });
        return Task.CompletedTask;
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(_ => new DaprClientBuilder().Build());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fps-booking", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Booking API"));

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
