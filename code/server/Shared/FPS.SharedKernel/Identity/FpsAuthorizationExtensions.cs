using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace FPS.SharedKernel.Identity;

public static class FpsAuthorizationExtensions
{
    // Registers tenant role mapping, deactivated-user tracking, and claims transformation,
    // then configures a default authorization policy that rejects deactivated users.
    // Call instead of AddAuthorization() in each FPS service Program.cs.
    public static IServiceCollection AddFpsAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<ITenantRoleMapper, ConfiguredTenantRoleMapper>();
        services.AddSingleton<IDeactivatedUserStore, InMemoryDeactivatedUserStore>();
        // Default: no-enforcement store (enforcement activates when Customer service registers tenants).
        // Services that host the Customer identity config replace this with InMemoryTenantIdentityConfigStore.
        services.AddSingleton<ITenantIdentityConfigStore, InMemoryTenantIdentityConfigStore>();
        services.AddScoped<IClaimsTransformation, TenantClaimsTransformation>();

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    ctx.User.FindFirstValue(TenantClaimsTransformation.DeactivatedClaim) != "true")
                .Build();
        });

        return services;
    }
}
