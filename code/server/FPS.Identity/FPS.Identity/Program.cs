var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddLogging();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

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

app.UseAuthorization();
app.MapControllers();
app.Run();
