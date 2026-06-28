using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Customer.Identity;

/// <summary>
/// PLAT001 tenant/platform authorization for the Customer admin surface.
///
/// A <c>platform_admin</c> (cross-tenant) may administer any tenant. A tenant
/// <c>admin</c> may administer only its own tenant. The cross-tenant capability is
/// safe because <see cref="TenantClaimsTransformation"/> guarantees a
/// <c>platform_*</c> role is present only on a token from the trusted platform
/// issuer — a customer-issuer token can never carry one.
/// </summary>
public static class TenantAccess
{
    public static bool IsPlatformAdmin(this ICurrentUser user) =>
        user.IsInRole(FpsRoles.PlatformAdmin);

    /// <summary>
    /// A platform operator (or an admin, who is a superset). Operators run day-to-day platform
    /// tasks such as onboarding triage; admins retain everything operators can do.
    /// </summary>
    public static bool IsPlatformOperator(this ICurrentUser user) =>
        user.IsInRole(FpsRoles.PlatformOperator) || user.IsPlatformAdmin();

    public static bool CanAdministerTenant(this ICurrentUser user, string? routeTenantId) =>
        user.IsPlatformAdmin()
        || (user.IsInRole(FpsRoles.Admin)
            && !string.IsNullOrEmpty(routeTenantId)
            && string.Equals(user.TenantId, routeTenantId, StringComparison.Ordinal));
}

/// <summary>
/// Authorization filter: the caller must be able to administer the tenant named by
/// the <c>{tenantId}</c> route value — <c>platform_admin</c> (cross-tenant) or the
/// tenant's own <c>admin</c>. A missing route tenant is rejected (fail closed).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireTenantAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!user.IsAuthenticated) { context.Result = new UnauthorizedResult(); return; }

        var tenantId = context.RouteData.Values["tenantId"] as string;
        if (string.IsNullOrEmpty(tenantId) || !user.CanAdministerTenant(tenantId))
            context.Result = new ForbidResult();
    }
}

/// <summary>
/// Authorization filter: the caller must be a cross-tenant <c>platform_admin</c>
/// (e.g. tenant creation). A tenant admin cannot perform platform-plane operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePlatformAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!user.IsAuthenticated) { context.Result = new UnauthorizedResult(); return; }

        if (!user.IsPlatformAdmin())
            context.Result = new ForbidResult();
    }
}

/// <summary>
/// Authorization filter: the caller must be a platform <c>operator</c> (or <c>admin</c>) — a
/// platform-plane role gated to the platform issuer. Used for cross-tenant operator surfaces such
/// as the onboarding triage queue; a tenant admin can never reach it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePlatformOperatorAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!user.IsAuthenticated) { context.Result = new UnauthorizedResult(); return; }

        if (!user.IsPlatformOperator())
            context.Result = new ForbidResult();
    }
}
