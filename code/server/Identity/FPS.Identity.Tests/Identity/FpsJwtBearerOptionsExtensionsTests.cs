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

    // ── PLAT001 hardening: signing keys are bound to the token's issuer ─────────
    // Guards the tenant→platform escalation path: a token whose `iss` claims one realm must
    // only be verifiable with THAT realm's keys, never the union of all realms' keys.

    private static SecurityKey Key(string id) =>
        new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes($"{id}-signing-key-at-least-32-bytes!!")) { KeyId = id };

    private static IReadOnlyDictionary<string, IReadOnlyCollection<SecurityKey>> TwoRealmKeys(
        out SecurityKey customerKey, out SecurityKey platformKey)
    {
        customerKey = Key("customer-kid");
        platformKey = Key("platform-kid");
        return new Dictionary<string, IReadOnlyCollection<SecurityKey>>(StringComparer.Ordinal)
        {
            [CustomerAuthority] = [customerKey],
            [PlatformAuthority] = [platformKey],
        };
    }

    [Fact]
    public void ResolveKeysForIssuer_PlatformIssuer_ReturnsOnlyPlatformKeys_NotCustomerKeys()
    {
        var map = TwoRealmKeys(out var customerKey, out var platformKey);

        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(PlatformAuthority, kid: null, map);

        // A token claiming the platform issuer can only be checked against the platform key,
        // so a customer-realm-signed token claiming iss=platform fails signature validation.
        Assert.Contains(platformKey, keys);
        Assert.DoesNotContain(customerKey, keys);
    }

    [Fact]
    public void ResolveKeysForIssuer_CustomerIssuer_ReturnsOnlyCustomerKeys()
    {
        var map = TwoRealmKeys(out var customerKey, out var platformKey);

        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(CustomerAuthority, kid: null, map);

        Assert.Contains(customerKey, keys);
        Assert.DoesNotContain(platformKey, keys);
    }

    [Fact]
    public void ResolveKeysForIssuer_TrailingSlashIssuer_StillBinds()
    {
        var map = TwoRealmKeys(out _, out var platformKey);

        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(PlatformAuthority + "/", kid: null, map);

        Assert.Contains(platformKey, keys);
    }

    [Theory]
    [InlineData("https://attacker.example/realms/evil")]
    [InlineData(null)]
    public void ResolveKeysForIssuer_UnknownOrMissingIssuer_ReturnsNoKeys(string? issuer)
    {
        var map = TwoRealmKeys(out _, out _);

        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(issuer, kid: null, map);

        Assert.Empty(keys);
    }

    [Fact]
    public void ResolveKeysForIssuer_WithKid_PrefersMatchingKey_ButStaysWithinIssuer()
    {
        var k1 = Key("kid-1");
        var k2 = Key("kid-2");
        var map = new Dictionary<string, IReadOnlyCollection<SecurityKey>>(StringComparer.Ordinal)
        {
            [PlatformAuthority] = [k1, k2],
        };

        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(PlatformAuthority, kid: "kid-2", map);

        Assert.Equal([k2], keys);
    }

    // ── Dev local-issuer host override preserved on top of the issuer-bound resolver ──────

    // Same Keycloak realm reached on a different host than the configured authority.
    private const string CustomerRealmAltHost = "https://192.168.1.10/realms/fairspot";

    [Fact]
    public void AliasLocalDevIssuer_SameRealmDifferentHost_ResolvesCustomerKeysOnly_NeverPlatform()
    {
        var map = TwoRealmKeys(out var customerKey, out var platformKey);
        string[] configured = [CustomerAuthority, PlatformAuthority];

        var aliased = FpsJwtBearerOptionsExtensions.AliasLocalDevIssuer(
            CustomerRealmAltHost, CustomerAuthority, allowLocalOverride: true, configured);
        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(aliased, kid: null, map);

        // The dev override resolves the customer realm's keys (so local-host tokens still work)
        // and never the platform realm's key — the security binding holds.
        Assert.Contains(customerKey, keys);
        Assert.DoesNotContain(platformKey, keys);
    }

    [Fact]
    public void AliasLocalDevIssuer_CrossRealmIssuer_IsNotAliased_ResolvesNoKeys()
    {
        var map = TwoRealmKeys(out _, out _);
        string[] configured = [CustomerAuthority, PlatformAuthority];
        const string foreignRealm = "https://192.168.1.10/realms/evil";

        var aliased = FpsJwtBearerOptionsExtensions.AliasLocalDevIssuer(
            foreignRealm, CustomerAuthority, allowLocalOverride: true, configured);
        var keys = FpsJwtBearerOptionsExtensions.ResolveKeysForIssuer(aliased, kid: null, map);

        Assert.Empty(keys);
    }

    [Fact]
    public void AliasLocalDevIssuer_OverrideDisabled_DoesNotAlias()
    {
        string[] configured = [CustomerAuthority, PlatformAuthority];

        var aliased = FpsJwtBearerOptionsExtensions.AliasLocalDevIssuer(
            CustomerRealmAltHost, CustomerAuthority, allowLocalOverride: false, configured);

        Assert.Equal(CustomerRealmAltHost, aliased);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
