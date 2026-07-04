using Dapr.Client;
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
builder.Services.AddDaprClient();
builder.Services.AddSingleton<DaprNotificationRepository>();
builder.Services.AddSingleton<INotificationRepository>(sp => sp.GetRequiredService<DaprNotificationRepository>());
builder.Services.AddSingleton<INotificationPreferencesRepository, DaprNotificationPreferencesRepository>();
builder.Services.AddSingleton<DaprHrRosterStore>();
builder.Services.AddSingleton<IHrRosterStore>(sp => sp.GetRequiredService<DaprHrRosterStore>());
builder.Services.AddSingleton<INotificationBroadcaster, InMemoryNotificationBroadcaster>();
builder.Services.Configure<DaprSendGridEmailOptions>(
    builder.Configuration.GetSection(DaprSendGridEmailOptions.SectionName));
var emailProvider = builder.Configuration["Notification:Email:Provider"];
if (DaprSendGridEmailNotificationSender.IsConfiguredProvider(emailProvider))
{
    builder.Services.AddSingleton<IEmailNotificationSender, DaprSendGridEmailNotificationSender>();
}
else
{
    builder.Services.AddSingleton<IEmailNotificationSender, InMemoryEmailNotificationSender>();
}
builder.Services.AddSingleton<INotificationAudienceResolver, RosterBackedAudienceResolver>();
builder.Services.AddSingleton<HrRosterConfigurationSeeder>();
builder.Services.AddScoped<NotificationTenantStorePurger>();
builder.Services.AddScoped<BookingEventNotificationHandler>();
builder.Services.AddScoped<TenantRequestSalesAlertHandler>();
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

builder.Services.AddFpsHealthChecks()
    .AddCheck<HrRosterPersistenceHealthCheck>("hr-roster-persistence", Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);
builder.Services.AddFpsObservability("fps-notification", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();
builder.Services.AddFpsDurableDeactivatedUserStore();

var app = builder.Build();

// Hydrate HR roster from Dapr before seeding from config so that
// a config-absent restart still restores the last known roster.
app.Services.GetRequiredService<DaprHrRosterStore>().HydrateAsync().GetAwaiter().GetResult();

// Populate the HR roster from configuration before the app starts
// serving traffic so the first event arriving after restart fans out
// correctly. Empty / missing config is a no-op (logged).
app.Services.GetRequiredService<HrRosterConfigurationSeeder>().Seed();

// SEC003 (#495): docs are gated to Development to reduce hosted recon surface.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Notification API"));
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
