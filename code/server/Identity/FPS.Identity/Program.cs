using FPS.Identity.Identity;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddLogging();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Identity API");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithSidebar(false);
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
