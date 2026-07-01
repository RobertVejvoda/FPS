using FPS.DataHub.Application;
using FPS.DataHub.Identity;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<DataHubDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DataHub")
            ?? throw new InvalidOperationException("ConnectionStrings:DataHub is required"),
        npgsql => npgsql.MigrationsAssembly(typeof(DataHubDbContext).Assembly.FullName));

    if (builder.Environment.IsDevelopment())
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<EventInboxService>();
builder.Services.AddScoped<IProjectionHandler, BookingProjectionHandler>();
// PLAT005A — registered after BookingProjectionHandler so the recompute sees the upstream
// outcome/draw write for the same event (dispatch is sequential in registration order).
builder.Services.AddScoped<IProjectionHandler, UsageStatsProjectionHandler>();

// PLAT003C — destructive single-tenant purge of DataHub read models (invoked over Dapr).
builder.Services.AddScoped<DataHubTenantStorePurger>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "DataHub API", Version = "v1" };
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

builder.Services.AddFpsHealthChecks()
    .AddDbContextCheck<DataHubDbContext>("datahub-db")
    .AddCheck<EventInboxHealthCheck>("datahub-event-inbox");

builder.Services.AddFpsObservability("fps-datahub", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();
builder.Services.AddFpsDurableDeactivatedUserStore();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
    await db.Database.MigrateAsync();
}

// SEC003 (#495): docs are gated to Development to reduce hosted recon surface.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("DataHub API"));
}
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
