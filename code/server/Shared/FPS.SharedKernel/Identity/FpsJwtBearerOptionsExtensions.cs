using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
        options.RequireHttpsMetadata = !environment.IsDevelopment();
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
