using FPS.Notification.Application;
using FPS.Notification.Identity;
using FPS.Notification.Infrastructure;
using FPS.SharedKernel.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
builder.Services.AddSingleton<INotificationBroadcaster, InMemoryNotificationBroadcaster>();
builder.Services.AddSingleton<IEmailNotificationSender, InMemoryEmailNotificationSender>();
builder.Services.AddScoped<BookingEventNotificationHandler>();
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
