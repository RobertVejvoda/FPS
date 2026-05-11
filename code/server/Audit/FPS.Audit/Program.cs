using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddDapr();
builder.Services.AddSingleton<IAuditRepository, InMemoryAuditRepository>();
builder.Services.AddScoped<BookingEventAuditHandler>();

var app = builder.Build();

app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.Run();

public partial class Program { }
