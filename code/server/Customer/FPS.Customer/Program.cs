using FPS.Customer.Application;
using FPS.Customer.Identity;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.HealthChecks;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Profile;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
builder.Services.AddSingleton<ITenantIdentityRepository, InMemoryTenantIdentityRepository>();
builder.Services.AddSingleton<ITenantParkingBootstrapRepository, InMemoryTenantParkingBootstrapRepository>();
builder.Services.AddSingleton<IEmployeeBootstrapRepository, InMemoryEmployeeBootstrapRepository>();
// NullProfileBootstrapSink: Customer service unit tests / standalone startup don't
// have the Profile service in-process. Production wires the Profile repo here.
builder.Services.AddSingleton<IProfileBootstrapSink, NullProfileBootstrapSink>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<TenantIdentityService>();
builder.Services.AddScoped<TenantParkingBootstrapService>();
builder.Services.AddScoped<EmployeeBootstrapService>();
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
app.Run();

public partial class Program { }
