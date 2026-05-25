using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
}
