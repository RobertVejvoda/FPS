using FPS.Notification.Application;
using FPS.Notification.Identity;
using FPS.Notification.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
using FPS.SharedKernel.Identity;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
builder.Services.AddSingleton<INotificationPreferencesRepository, InMemoryNotificationPreferencesRepository>();
builder.Services.AddSingleton<INotificationBroadcaster, InMemoryNotificationBroadcaster>();
builder.Services.AddSingleton<IEmailNotificationSender, InMemoryEmailNotificationSender>();
builder.Services.AddSingleton<IHrRosterStore, InMemoryHrRosterStore>();
builder.Services.AddSingleton<INotificationAudienceResolver, RosterBackedAudienceResolver>();
builder.Services.AddSingleton<HrRosterConfigurationSeeder>();
builder.Services.AddScoped<BookingEventNotificationHandler>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "Notification API", Version = "v1" };
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

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fps-notification", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();

var app = builder.Build();

// Populate the HR roster from configuration before the app starts
// serving traffic so the first event arriving after restart fans out
// correctly. Empty / missing config is a no-op (logged).
app.Services.GetRequiredService<HrRosterConfigurationSeeder>().Seed();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Notification API"));
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
