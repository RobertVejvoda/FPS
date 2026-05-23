using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Identity;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
builder.Services.AddSingleton<ITenantIdentityRepository, InMemoryTenantIdentityRepository>();
builder.Services.AddSingleton<ITenantParkingBootstrapRepository, InMemoryTenantParkingBootstrapRepository>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<TenantIdentityService>();
builder.Services.AddScoped<TenantParkingBootstrapService>();
builder.Services.AddScoped<TenantReadinessService>();
builder.Services.AddSingleton<IProfileReadinessProbe, NoOpProfileReadinessProbe>();
builder.Services.AddSingleton<IBookingReadinessProbe, NoOpBookingReadinessProbe>();
builder.Services.AddSingleton<INotificationReadinessProbe, NoOpNotificationReadinessProbe>();
builder.Services.AddSingleton<IAuditReadinessProbe, NoOpAuditReadinessProbe>();
builder.Services.AddSingleton<IReportingReadinessProbe, NoOpReportingReadinessProbe>();
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
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        options.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
    });

builder.Services.AddFpsHealthChecks();
builder.Services.AddFpsAuthorization();
// Override the default ITenantIdentityConfigStore with the concrete singleton so
// TenantIdentityService can call Register() and TenantClaimsTransformation can enforce it.
var identityConfigStore = new InMemoryTenantIdentityConfigStore();
builder.Services.AddSingleton<ITenantIdentityConfigStore>(identityConfigStore);
builder.Services.AddSingleton(identityConfigStore);

// Replace ConfiguredTenantRoleMapper with the API-backed store. When a tenant is
// registered in the config store, only explicitly mapped roles are passed through.
// Unconfigured tenants fall back to pass-through (backward-compatible).
var roleMappingStore = new InMemoryTenantRoleMappingStore(identityConfigStore);
builder.Services.AddSingleton<ITenantRoleMapper>(roleMappingStore);
builder.Services.AddSingleton(roleMappingStore);

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Customer API"));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFpsHealthChecks();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await SeedLocalDemoTenantAsync(scope.ServiceProvider);
}

app.Run();

static async Task SeedLocalDemoTenantAsync(IServiceProvider services)
{
    const string tenantId = "tenant-1";

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
    bootstrap.RecordLocation("LOC-MAIN", activeSlotCount: 10, hasLocationPolicy: false, "local-seed");
    await parkingRepository.SaveAsync(bootstrap, CancellationToken.None);
}

public partial class Program { }
