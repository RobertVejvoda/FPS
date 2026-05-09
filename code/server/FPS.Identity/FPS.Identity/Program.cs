var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddLogging();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddScalar();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => 
	{
		options.WithTitle("Identity API");
		options.WithTheme(ScalarTheme.BluePlanet);
		options.WithSidebar(false);	
	});
	app.UseScalar();
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();