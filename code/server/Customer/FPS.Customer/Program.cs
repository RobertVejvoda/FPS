using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Observability;
using FPS.SharedKernel.Identity;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDaprClient();
builder.Services.AddSingleton<ITenantRepository, DaprCustomerTenantRepository>();
builder.Services.AddSingleton<ITenantIdentityRepository, DaprCustomerIdentityRepository>();
builder.Services.AddSingleton<ITenantParkingBootstrapRepository, DaprCustomerParkingBootstrapRepository>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<TenantIdentityService>();
builder.Services.AddScoped<TenantParkingBootstrapService>();
builder.Services.AddScoped<TenantReadinessService>();
builder.Services.AddScoped<TenantDemoSeedService>();
builder.Services.AddHttpClient<IDemoSeedProfileClient, HttpDemoSeedProfileClient>();
builder.Services.AddHttpClient<IDemoSeedConfigurationClient, HttpDemoSeedConfigurationClient>();
builder.Services.AddHttpClient<IProfileReadinessProbe, HttpProfileReadinessProbe>();
builder.Services.AddHttpClient<IBookingReadinessProbe, HttpBookingReadinessProbe>();
builder.Services.AddHttpClient<INotificationReadinessProbe, HttpNotificationReadinessProbe>();
builder.Services.AddHttpClient<IAuditReadinessProbe, HttpAuditReadinessProbe>();
builder.Services.AddHttpClient<IReportingReadinessProbe, HttpReportingReadinessProbe>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "Customer API", Version = "v1" };
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

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.ConfigureFpsJwtBearer(builder.Configuration, builder.Environment);
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsObservability("fps-customer", builder.Configuration);
builder.Services.AddFpsMetrics();
builder.Services.AddFpsAuthorization();
builder.Services.AddFpsDurableDeactivatedUserStore();
// Override the default ITenantIdentityConfigStore with the concrete singleton so
// TenantIdentityService can call Register() and TenantClaimsTransformation can enforce it.
var identityConfigStore = new InMemoryTenantIdentityConfigStore();
builder.Services.AddSingleton<ITenantIdentityConfigStore>(identityConfigStore);
builder.Services.AddSingleton(identityConfigStore);

// Replace ConfiguredTenantRoleMapper with the API-backed store. When a tenant is
// registered in the config store, only explicitly mapped roles are passed through.
// Unconfigured tenants fall back to pass-through (backward-compatible).
var roleMappingStore = new InMemoryTenantRoleMappingStore(identityConfigStore, builder.Configuration);
builder.Services.AddSingleton<ITenantRoleMapper>(roleMappingStore);
builder.Services.AddSingleton(roleMappingStore);
builder.Services.AddSingleton<IdentityStoreHydrator>();

var app = builder.Build();

// SEC003 (#495): docs are gated to Development to reduce hosted recon surface.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Customer API"));
}
app.UseFpsMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseFpsRequestTraceLogging();
app.MapFpsMetrics();
app.MapFpsHealthChecks();

// Wait for Dapr sidecar before hydrating. Guard skips the poll in test runs and local dev
// without a sidecar (DAPR_HTTP_PORT absent) to avoid 5-minute startup timeouts.
var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
if (!string.IsNullOrEmpty(daprHttpPort))
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var healthUrl = $"http://localhost:{daprHttpPort}/v1.0/healthz/outbound";
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    for (var attempt = 1; attempt <= 60; attempt++)
    {
        try
        {
            var resp = await http.GetAsync(healthUrl);
            if (resp.IsSuccessStatusCode)
            {
                startupLogger.LogInformation("Dapr sidecar outbound health ready after {Attempt} attempt(s).", attempt);
                break;
            }
        }
        catch { /* sidecar not yet listening */ }
        startupLogger.LogWarning("Waiting for Dapr outbound health on {Url} (attempt {Attempt}/60)…", healthUrl, attempt);
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}

// PERSIST006B startup gate: IdentityStoreHydrator.HydrateAsync propagates exceptions in
// non-Development profiles, crashing the process before app.Run() so the orchestrator
// restarts the pod when Dapr is unavailable — preventing the fail-open path in
// TenantClaimsTransformation (IsEnforcementActive==false passes raw roles with empty stores).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IdentityStoreHydrator>().HydrateAsync();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try { await SeedLocalDemoTenantAsync(scope.ServiceProvider); }
    catch (Exception ex) { seedLogger.LogError(ex, "Demo tenant seed failed; skipping"); }
    try { await SeedGreenLogisticsTenantAsync(scope.ServiceProvider); }
    catch (Exception ex) { seedLogger.LogError(ex, "GL tenant seed failed; skipping"); }
}

app.Run();


static async Task SeedLocalDemoTenantAsync(IServiceProvider services)
{
    // FPS_DEMO_TENANT_ID overrides the default demo tenant for local experiments.
    var config = services.GetRequiredService<IConfiguration>();
    var tenantId = config["FPS_DEMO_TENANT_ID"] ?? "demo";

    var tenantRepository = services.GetRequiredService<ITenantRepository>();
    if (await tenantRepository.GetAsync(tenantId, CancellationToken.None) is not null)
    {
        return;
    }

    var now = DateTimeOffset.UtcNow;
    var tenant = new TenantWorkspace
    {
        TenantId = tenantId,
        Slug = "demo-company",
        DisplayName = "Demo Company",
        Region = "CZ",
        TimeZone = "Europe/Prague",
        Kind = TenantKind.Sandbox,
        SupportContacts =
        [
            new TenantSupportContact("Demo Facilities", "facilities@example.local", "Facilities"),
            new TenantSupportContact("Demo IT", "it@example.local", "Identity")
        ],
        Provisioning = TenantProvisioningMetadata.Generate(tenantId, "demo-company"),
        CreatedAt = now,
    };
    tenant.TryTransition(TenantLifecycleState.Configured, "local-seed", "Local demo tenant setup", "Development seed");
    tenant.TryTransition(TenantLifecycleState.Seeded, "local-seed", "Local demo seed data available", "Development seed");
    await tenantRepository.SaveAsync(tenant, CancellationToken.None);

    var identityRepository = services.GetRequiredService<ITenantIdentityRepository>();
    await identityRepository.SaveConfigAsync(new TenantIdentityConfig
    {
        TenantId = tenantId,
        TrustedIssuer = "http://localhost:8180/realms/fps-local",
        Audience = "fps-api",
        TenantClaimName = "tenant_id",
        SubjectClaimName = "sub",
        RoleClaimNames = ["roles"],
        RoleMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employee"] = "employee",
            ["hr_manager"] = "hr_manager",
            ["admin"] = "admin",
            ["report_viewer"] = "report_viewer",
        },
        LocalAccountPolicyEnabled = true,
        ConfiguredByHash = "local-seed",
        ConfiguredAt = now,
    }, CancellationToken.None);
    await identityRepository.SaveAdminAsync(new TenantAdminRecord(
        tenantId,
        "local-seed-tenant-admin",
        TenantAdminType.Local,
        "local-seed",
        now,
        "Local development tenant administrator.",
        IsActive: true), CancellationToken.None);

    var parkingRepository = services.GetRequiredService<ITenantParkingBootstrapRepository>();
    var bootstrap = await parkingRepository.GetOrCreateAsync(tenantId, CancellationToken.None);
    bootstrap.RecordDefaultPolicy(new BootstrapPolicySnapshot(
        "Europe/Prague",
        "18:00",
        100,
        30,
        "local-seed",
        now));
    bootstrap.RecordLocation("Prague", activeSlotCount: 15, hasLocationPolicy: false, "local-seed");
    await parkingRepository.SaveAsync(bootstrap, CancellationToken.None);
}

static async Task SeedGreenLogisticsTenantAsync(IServiceProvider services)
{
    const string tenantId = "greenlogistics";
    const string slug = "greenlogistics";
    const string discoveryDomain = "greenlogistics.example";

    var tenantRepository = services.GetRequiredService<ITenantRepository>();
    if (await tenantRepository.GetAsync(tenantId, CancellationToken.None) is not null)
        return;

    var now = DateTimeOffset.UtcNow;
    var tenant = new TenantWorkspace
    {
        TenantId = tenantId,
        Slug = slug,
        DisplayName = "Green Logistics",
        Region = "EU",
        TimeZone = "Europe/Prague",
        Kind = TenantKind.Sandbox,
        SupportContacts =
        [
            new TenantSupportContact("GL Facilities", "facilities@greenlogistics.example", "Facilities"),
            new TenantSupportContact("GL IT Support", "it@greenlogistics.example", "Identity"),
        ],
        Provisioning = TenantProvisioningMetadata.Generate(tenantId, slug),
        CreatedAt = now,
    };

    tenant.SetBranding(new TenantBrandingConfig
    {
        PrimaryColor = "#2e7d32",
        AccentColor = "#a5d6a7",
        LoginMode = TenantLoginMode.Both,
    });
    tenant.AddDiscoveryDomain(discoveryDomain, "local-seed");

    tenant.TryTransition(TenantLifecycleState.Configured, "local-seed", "Green Logistics demo tenant setup", "Development seed");
    tenant.TryTransition(TenantLifecycleState.Seeded, "local-seed", "Green Logistics demo seed data available", "Development seed");

    await tenantRepository.SaveAsync(tenant, CancellationToken.None);

    var identityRepository = services.GetRequiredService<ITenantIdentityRepository>();
    await identityRepository.SaveConfigAsync(new TenantIdentityConfig
    {
        TenantId = tenantId,
        TrustedIssuer = "http://localhost:8180/realms/fps-local",
        Audience = "fps-api",
        TenantClaimName = "tenant_id",
        SubjectClaimName = "sub",
        RoleClaimNames = ["roles"],
        RoleMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employee"] = "employee",
            ["hr_manager"] = "hr_manager",
            ["admin"] = "admin",
            ["report_viewer"] = "report_viewer",
            ["auditor"] = "auditor",
        },
        LocalAccountPolicyEnabled = true,
        ConfiguredByHash = "local-seed",
        ConfiguredAt = now,
    }, CancellationToken.None);

    await identityRepository.SaveAdminAsync(new TenantAdminRecord(
        tenantId,
        "local-seed-gl-admin",
        TenantAdminType.Local,
        "local-seed",
        now,
        "Green Logistics development tenant administrator.",
        IsActive: true), CancellationToken.None);

    var parkingRepository = services.GetRequiredService<ITenantParkingBootstrapRepository>();
    var bootstrap = await parkingRepository.GetOrCreateAsync(tenantId, CancellationToken.None);
    bootstrap.RecordDefaultPolicy(new BootstrapPolicySnapshot(
        "Europe/Prague",
        "18:00",
        50,
        30,
        "local-seed",
        now));
    bootstrap.RecordLocation("GL-HQ", activeSlotCount: 20, hasLocationPolicy: false, "local-seed");
    await parkingRepository.SaveAsync(bootstrap, CancellationToken.None);
}

public partial class Program { }
