using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Identity;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();

// Share the same in-memory instance for both append and query interfaces.
var inMemoryAuditRepo = new InMemoryAuditRepository();
builder.Services.AddSingleton<IAuditRepository>(inMemoryAuditRepo);
builder.Services.AddSingleton<IAuditQueryRepository>(inMemoryAuditRepo);
builder.Services.AddSingleton<IPiiMappingRepository, InMemoryPiiMappingRepository>();

builder.Services.AddScoped<BookingEventAuditHandler>();
builder.Services.AddScoped<AuditQueryService>();
builder.Services.AddScoped<PiiErasureService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        options.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.Run();

public partial class Program { }
