using Dapr.Client;
using FPS.SharedKernel.Infrastructure;
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

    // Replaces the InMemoryDeactivatedUserStore (registered by AddFpsAuthorization) with the
    // Dapr-backed DaprDeactivatedUserStore. Call after AddFpsAuthorization() in every service
    // that has a Dapr sidecar so deactivated-user state survives service restarts and is shared
    // across all instances via the "deactivatedstore" Dapr component (no scope restriction).
    public static IServiceCollection AddFpsDurableDeactivatedUserStore(
        this IServiceCollection services,
        string storeName = "deactivatedstore")
    {
        services.AddSingleton<IDeactivatedUserStore>(sp =>
            new DaprDeactivatedUserStore(sp.GetRequiredService<DaprClient>(), storeName));
        return services;
    }
}
