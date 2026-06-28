using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

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

    [Fact]
    public void ConfigureFpsJwtBearer_InDevelopment_AllowsSameRealmIssuerHostOverrideWhenEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8180/realms/fps-local",
                ["Auth:Audience"] = "fps-mobile-dev",
                ["Auth:AllowLocalIssuerHostOverride"] = "true",
            })
            .Build();
        var options = new JwtBearerOptions();

        options.ConfigureFpsJwtBearer(configuration, new FakeHostEnvironment("Development"));

        Assert.NotNull(options.TokenValidationParameters.IssuerValidator);
        var issuer = options.TokenValidationParameters.IssuerValidator!(
            "http://192.168.1.10:8180/realms/fps-local",
            null,
            new TokenValidationParameters());
        Assert.Equal("http://192.168.1.10:8180/realms/fps-local", issuer);
    }

    [Fact]
    public void ConfigureFpsJwtBearer_InProduction_AllowsHttpMetadataOnlyWhenExplicitlyEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://keycloak:8080/realms/fps-local",
                ["Auth:Audience"] = "fps-mobile-dev",
                ["Auth:AllowHttpMetadata"] = "true",
            })
            .Build();
        var options = new JwtBearerOptions();

        options.ConfigureFpsJwtBearer(configuration, new FakeHostEnvironment("Production"));

        Assert.False(options.RequireHttpsMetadata);
    }

    [Fact]
    public void ConfigureFpsJwtBearer_DoesNotAllowDifferentRealmIssuerHostOverride()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8180/realms/fps-local",
                ["Auth:Audience"] = "fps-mobile-dev",
                ["Auth:AllowLocalIssuerHostOverride"] = "true",
            })
            .Build();
        var options = new JwtBearerOptions();

        options.ConfigureFpsJwtBearer(configuration, new FakeHostEnvironment("Development"));

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            options.TokenValidationParameters.IssuerValidator!(
                "http://192.168.1.10:8180/realms/other",
                null,
                new TokenValidationParameters()));
    }

    // ── PLAT001 multi-issuer (platform realm) ───────────────────────────────────

    private const string CustomerAuthority = "https://auth.example/realms/fairspot";
    private const string PlatformAuthority = "https://platform.example/realms/fps-platform";

    private static JwtBearerOptions Configure(Dictionary<string, string?> values, string environment = "Production")
    {
        var options = new JwtBearerOptions();
        options.ConfigureFpsJwtBearer(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new FakeHostEnvironment(environment));
        return options;
    }

    [Fact]
    public void PlatformAuthoritySet_AddsBothValidIssuers()
    {
        var options = Configure(new()
        {
            ["Auth:Authority"] = CustomerAuthority,
            ["Auth:PlatformAuthority"] = PlatformAuthority,
        });

        var issuers = options.TokenValidationParameters.ValidIssuers?.ToList();
        Assert.NotNull(issuers);
        Assert.Contains(CustomerAuthority, issuers!);
        Assert.Contains(PlatformAuthority, issuers!);
    }

    [Fact]
    public void PlatformAuthoritySet_ConfiguresSigningKeyResolver()
    {
        var options = Configure(new()
        {
            ["Auth:Authority"] = CustomerAuthority,
            ["Auth:PlatformAuthority"] = PlatformAuthority,
        });

        Assert.NotNull(options.TokenValidationParameters.IssuerSigningKeyResolver);
    }

    [Fact]
    public void PlatformAuthorityUnset_DoesNotActivateMultiIssuer()
    {
        var options = Configure(new() { ["Auth:Authority"] = CustomerAuthority });

        Assert.Null(options.TokenValidationParameters.ValidIssuers);
        Assert.Null(options.TokenValidationParameters.IssuerSigningKeyResolver);
    }

    [Fact]
    public void PlatformAuthoritySet_TrailingSlashIsNormalized()
    {
        var options = Configure(new()
        {
            ["Auth:Authority"] = CustomerAuthority + "/",
            ["Auth:PlatformAuthority"] = PlatformAuthority + "/",
        });

        var issuers = options.TokenValidationParameters.ValidIssuers?.ToList();
        Assert.NotNull(issuers);
        Assert.Contains(CustomerAuthority, issuers!);
        Assert.Contains(PlatformAuthority, issuers!);
    }

    [Fact]
    public void LocalIssuerOverride_StillApplies_WithPlatformAuthority_InDevelopment()
    {
        var options = Configure(new()
        {
            ["Auth:Authority"] = CustomerAuthority,
            ["Auth:PlatformAuthority"] = PlatformAuthority,
            ["Auth:AllowLocalIssuerHostOverride"] = "true",
        }, environment: "Development");

        // The dev local-issuer override is preserved, and the platform issuer is still added.
        Assert.NotNull(options.TokenValidationParameters.IssuerValidator);
        Assert.Contains(PlatformAuthority, options.TokenValidationParameters.ValidIssuers!);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
