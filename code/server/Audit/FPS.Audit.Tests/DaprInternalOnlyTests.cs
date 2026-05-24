using FPS.Audit.Application.Privacy;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Audit.Tests;

public sealed class DaprInternalOnlyTests
{
    private const string TokenHeader = "dapr-api-token";
    private const string ConfigKey = "DAPR_API_TOKEN";

    private static ResourceExecutingContext MakeContext(string? configuredToken, string? incomingHeader)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredToken is null
                ? []
                : new Dictionary<string, string?> { [ConfigKey] = configuredToken })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (incomingHeader is not null)
            httpContext.Request.Headers[TokenHeader] = incomingHeader;

        var actionContext = new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        return new ResourceExecutingContext(actionContext, [], []);
    }

    [Fact]
    public void NoTokenConfigured_AllowsThrough()
    {
        var ctx = MakeContext(configuredToken: null, incomingHeader: null);
        new DaprInternalOnlyAttribute().OnResourceExecuting(ctx);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void CorrectTokenPresent_AllowsThrough()
    {
        var ctx = MakeContext("secret-token", "secret-token");
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
