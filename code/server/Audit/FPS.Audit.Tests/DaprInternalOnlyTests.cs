using FPS.Audit.Application.Privacy;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FPS.Audit.Tests;

public sealed class DaprInternalOnlyTests
{
    private const string TokenHeader = "dapr-api-token";
    private const string ConfigKey = "APP_API_TOKEN";

    private static ResourceExecutingContext MakeContext(
        string? configuredToken, string? incomingHeader, string environmentName = "Development")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredToken is null
                ? []
                : new Dictionary<string, string?> { [ConfigKey] = configuredToken })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (incomingHeader is not null)
            httpContext.Request.Headers[TokenHeader] = incomingHeader;

        var actionContext = new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        return new ResourceExecutingContext(actionContext, [], []);
    }

    [Fact]
    public void NoTokenConfigured_Development_AllowsThrough()
    {
        // Local harness convenience: ASPNETCORE_ENVIRONMENT=Development keeps
        // the no-token path open so smoke tests don't require ceremony.
        var ctx = MakeContext(configuredToken: null, incomingHeader: null, environmentName: "Development");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void NoTokenConfigured_Production_Returns503()
    {
        // SEC002 (#494): a forgotten APP_API_TOKEN in a hosted profile must
        // not silently open the door. Outside Development the filter fails
        // closed with 503 so the misconfig is loud.
        var ctx = MakeContext(configuredToken: null, incomingHeader: null, environmentName: "Production");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void NoTokenConfigured_StagingOrHosted_Returns503()
    {
        var ctx = MakeContext(configuredToken: null, incomingHeader: null, environmentName: "Staging");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void NoTokenConfigured_TestEnvironment_Returns503()
    {
        // The Test environment is not Development either — tests that need
        // pass-through must configure the token, just like production does.
        var ctx = MakeContext(configuredToken: null, incomingHeader: null, environmentName: "Test");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        Assert.IsType<ObjectResult>(ctx.Result);
    }

    [Fact]
    public void NoHostEnvironment_NoTokenConfigured_Returns503()
    {
        // Codex review on PR #497: a broken / custom DI setup without
        // IHostEnvironment must be treated as unknown environment and
        // fail closed. The previous "env is null OR Development" branch
        // would have silently reopened every internal-only endpoint.
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        // Intentionally do NOT register IHostEnvironment.
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var ctx = new ResourceExecutingContext(actionContext, [], []);

        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void CorrectTokenPresent_AllowsThrough()
    {
        // Token configured + matching header: pass regardless of environment.
        var ctx = MakeContext("secret-token", "secret-token", environmentName: "Production");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void MissingToken_Returns403()
    {
        var ctx = MakeContext("secret-token", incomingHeader: null);
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void WrongToken_Returns403()
    {
        var ctx = MakeContext("secret-token", "wrong-token");
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void EmptyToken_Returns403()
    {
        var ctx = MakeContext("secret-token", string.Empty);
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(403, result.StatusCode);
    }

    private sealed class TestHostEnvironment(string envName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = envName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    // ── LegalBasis validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("gdpr-article-17", true)]
    [InlineData("ccpa-deletion-request", true)]
    [InlineData("consent-withdrawn", true)]
    [InlineData("GDPR-Article-17", true)]   // case-insensitive
    [InlineData("free text reason", false)]
    [InlineData("alice@example.com", false)]
    [InlineData("John Smith", false)]
    [InlineData("", false)]
    [InlineData("ABC-123", false)]
    public void LegalBasis_IsValid_MatchesAllowlist(string basis, bool expected)
    {
        var req = new CreateErasureRequest("user-1", basis);
        Assert.Equal(expected, req.IsValidLegalBasis);
    }

    [Fact]
    public void LegalBasis_AllowedSet_ContainsNoWhitespace()
    {
        foreach (var basis in CreateErasureRequest.AllowedLegalBases)
            Assert.DoesNotContain(' ', basis);
    }
}
