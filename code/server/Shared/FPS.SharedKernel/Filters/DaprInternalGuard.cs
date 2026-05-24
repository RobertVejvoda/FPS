using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace FPS.SharedKernel.Filters;

/// <summary>
/// Validates the Dapr app API token on inbound service-invocation calls.
/// In production, set APP_API_TOKEN on each app; the Dapr sidecar injects the
/// dapr-api-token header on all calls it forwards to the app. External callers
/// without a Dapr sidecar cannot pass this check.
/// If APP_API_TOKEN is not configured (local harness without token), the filter
/// allows through — configure the token in all non-dev environments.
/// See: https://docs.dapr.io/operations/security/app-api-token/
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DaprInternalOnlyAttribute : Attribute, IResourceFilter
{
    private const string DaprTokenHeader = "dapr-api-token";
    private const string ConfigKey = "APP_API_TOKEN";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var config = context.HttpContext.RequestServices
            .GetService(typeof(IConfiguration)) as IConfiguration;

        var expectedToken = config?[ConfigKey];

        // No token configured: allow (local harness without app token).
        if (string.IsNullOrEmpty(expectedToken))
            return;

        var incomingToken = context.HttpContext.Request.Headers[DaprTokenHeader].FirstOrDefault();
        if (incomingToken != expectedToken)
            context.Result = new ObjectResult("Forbidden: Dapr app token required.")
                { StatusCode = 403 };
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
