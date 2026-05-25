using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FPS.Identity.Tests.Identity;

public sealed class FpsJwtBearerOptionsExtensionsTests
{
    [Fact]
    public void ConfigureFpsJwtBearer_AcceptsPrimaryAndAdditionalAudiences()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8180/realms/fps-local",
                ["Auth:Audience"] = "fps-mobile-dev",
                ["Auth:AdditionalAudiences"] = "fps-web-dev, fps-cli-dev",
            })
            .Build();
        var options = new JwtBearerOptions();

        options.ConfigureFpsJwtBearer(configuration, new FakeHostEnvironment("Development"));

        Assert.Equal("http://localhost:8180/realms/fps-local", options.Authority);
        Assert.Equal("fps-mobile-dev", options.Audience);
        Assert.False(options.RequireHttpsMetadata);
        Assert.Contains("fps-mobile-dev", options.TokenValidationParameters.ValidAudiences);
        Assert.Contains("fps-web-dev", options.TokenValidationParameters.ValidAudiences);
        Assert.Contains("fps-cli-dev", options.TokenValidationParameters.ValidAudiences);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
