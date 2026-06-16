using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FPS.SharedKernel.Filters;

/// <summary>
/// Validates the Dapr app API token on inbound service-invocation calls.
/// In production, set APP_API_TOKEN on each app; the Dapr sidecar injects the
/// dapr-api-token header on all calls it forwards to the app. External callers
/// without a Dapr sidecar cannot pass this check.
///
/// SEC002 (#494): when APP_API_TOKEN is NOT configured, the filter fails
/// closed (503 Service Unavailable) unless the host environment is
/// Development. This prevents a hosted profile from silently shipping with
/// no token and accepting unauthenticated traffic. Local harness keeps
/// running because the dev environment uses ASPNETCORE_ENVIRONMENT=Development.
///
/// See: https://docs.dapr.io/operations/security/app-api-token/
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DaprInternalOnlyAttribute : Attribute, IResourceFilter
{
    private const string DaprTokenHeader = "dapr-api-token";
    private const string ConfigKey = "APP_API_TOKEN";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var services = context.HttpContext.RequestServices;
        var config = services.GetService(typeof(IConfiguration)) as IConfiguration;
        var env = services.GetService(typeof(IHostEnvironment)) as IHostEnvironment;

        var expectedToken = config?[ConfigKey];

        if (string.IsNullOrEmpty(expectedToken))
        {
            // No token configured: only permitted in an explicitly resolved
            // Development environment. A missing IHostEnvironment (broken
            // or custom DI setup) is treated as unknown and fails closed —
            // the previous "env is null OR Development" branch silently
            // reopened every internal-only endpoint (Codex review on PR #497).
            if (env?.IsDevelopment() == true)
                return;

            (services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?
                .CreateLogger("FPS.DaprInternalGuard")
                .LogWarning(
                    "Refusing internal-only request because APP_API_TOKEN is not configured. Path={Path} Env={Environment}",
                    context.HttpContext.Request.Path, env?.EnvironmentName ?? "unknown");

            context.Result = new ObjectResult("Internal Dapr token not configured for this environment.")
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        var incomingToken = context.HttpContext.Request.Headers[DaprTokenHeader].FirstOrDefault();
        if (incomingToken != expectedToken)
            context.Result = new ObjectResult("Forbidden: Dapr app token required.")
                { StatusCode = StatusCodes.Status403Forbidden };
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
