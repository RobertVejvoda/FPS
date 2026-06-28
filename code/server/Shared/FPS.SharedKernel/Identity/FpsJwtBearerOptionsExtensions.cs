using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FPS.SharedKernel.Identity;

public static class FpsJwtBearerOptionsExtensions
{
    public static void ConfigureFpsJwtBearer(
        this JwtBearerOptions options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var primaryAudience = configuration["Auth:Audience"];
        var audiences = ReadAudiences(configuration, primaryAudience);

        options.Authority = configuration["Auth:Authority"];
        options.Audience = primaryAudience;
        options.RequireHttpsMetadata = !environment.IsDevelopment()
            && !IsTruthy(configuration["Auth:AllowHttpMetadata"]);
        options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        options.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
        if (audiences.Count > 0)
        {
            options.TokenValidationParameters.ValidAudiences = audiences;
        }
        if (environment.IsDevelopment() && IsTruthy(configuration["Auth:AllowLocalIssuerHostOverride"]))
        {
            var authority = options.Authority;
            options.TokenValidationParameters.IssuerValidator = (issuer, token, parameters) =>
            {
                if (IsSameRealmIssuer(authority, issuer))
                {
                    return issuer;
                }

                return Validators.ValidateIssuer(issuer, token, parameters);
            };
        }

        ConfigurePlatformIssuer(options, configuration, environment);
    }

    // PLAT001 (Target B): when a platform realm is configured (Auth:PlatformAuthority),
    // accept tokens from BOTH the customer realm (Auth:Authority) and the platform realm.
    // Signing keys are resolved from whichever realm minted the token, and both realm
    // issuers are valid. TenantClaimsTransformation then confines platform_* roles to the
    // platform issuer (Auth:PlatformIssuer == the platform realm URL). When
    // Auth:PlatformAuthority is unset the platform plane is dormant and this is a no-op —
    // single-issuer behaviour is unchanged.
    private static void ConfigurePlatformIssuer(
        JwtBearerOptions options, IConfiguration configuration, IHostEnvironment environment)
    {
        var platformAuthority = configuration["Auth:PlatformAuthority"];
        if (string.IsNullOrWhiteSpace(platformAuthority)) return;

        var requireHttps = !environment.IsDevelopment() && !IsTruthy(configuration["Auth:AllowHttpMetadata"]);

        var authorities = new[] { options.Authority, platformAuthority }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!.TrimEnd('/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var managers = authorities
            .Select(a => new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{a}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = requireHttps }))
            .ToArray();

        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;
        tvp.ValidIssuers = authorities;
        // Resolve signing keys from every configured realm; a token validates if any
        // realm's current JWKS holds its signing key. A temporarily unreachable realm
        // does not block validation against the others.
        tvp.IssuerSigningKeyResolver = (_, _, _, _) =>
        {
            var keys = new List<SecurityKey>();
            foreach (var manager in managers)
            {
                try
                {
                    keys.AddRange(manager.GetConfigurationAsync(CancellationToken.None)
                        .GetAwaiter().GetResult().SigningKeys);
                }
                catch
                {
                    // realm metadata unreachable right now — other realms can still validate
                }
            }
            return keys;
        };
    }

    private static IReadOnlyList<string> ReadAudiences(IConfiguration configuration, string? primaryAudience)
    {
        var audiences = new List<string>();
        AddAudience(audiences, primaryAudience);

        var additional = configuration["Auth:AdditionalAudiences"];
        if (!string.IsNullOrWhiteSpace(additional))
        {
            foreach (var audience in additional.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddAudience(audiences, audience);
            }
        }

        foreach (var child in configuration.GetSection("Auth:AdditionalAudiences").GetChildren())
        {
            AddAudience(audiences, child.Value);
        }

        return audiences;
    }

    private static void AddAudience(List<string> audiences, string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience)) return;
        if (!audiences.Contains(audience, StringComparer.Ordinal))
        {
            audiences.Add(audience);
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameRealmIssuer(string? authority, string? issuer)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)) return false;
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)) return false;
        return string.Equals(authorityUri.AbsolutePath.TrimEnd('/'), issuerUri.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal)
            && authorityUri.AbsolutePath.Contains("/realms/", StringComparison.Ordinal);
    }
}
