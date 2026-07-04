using FPS.Notification.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FPS.Notification.Tests.Infrastructure;

// NOTIF #728 — resolves recipients to verified emails; sales/onboarding recipients that are already
// email addresses pass through without a Profile lookup; any lookup failure fails closed.
public sealed class DaprEmailRecipientResolverTests
{
    private readonly Mock<IProfileRecipientLookup> lookup = new();
    private readonly DaprEmailRecipientResolver resolver;

    public DaprEmailRecipientResolverTests() =>
        resolver = new DaprEmailRecipientResolver(lookup.Object, NullLogger<DaprEmailRecipientResolver>.Instance);

    [Fact]
    public async Task Resolve_RecipientAlreadyEmail_PassesThroughWithoutProfileLookup()
    {
        var result = await resolver.ResolveAsync("platform", "sales@fairspot.net");

        Assert.True(result.Resolved);
        Assert.Equal("sales@fairspot.net", result.Email);
        lookup.Verify(l => l.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_ProfileReturnsVerifiedEmail_ReturnsOk()
    {
        lookup.Setup(l => l.LookupAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileRecipientResult(true, "jan@greenlogistics.example", null));

        var result = await resolver.ResolveAsync("tenant-1", "user-1");

        Assert.True(result.Resolved);
        Assert.Equal("jan@greenlogistics.example", result.Email);
    }

    [Fact]
    public async Task Resolve_ProfileRejects_ReturnsRejectionReason()
    {
        lookup.Setup(l => l.LookupAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileRecipientResult(false, null, "email_unverified_source"));

        var result = await resolver.ResolveAsync("tenant-1", "user-1");

        Assert.False(result.Resolved);
        Assert.Null(result.Email);
        Assert.Equal("email_unverified_source", result.RejectionReason);
    }

    [Fact]
    public async Task Resolve_ProfileUnavailable_FailsClosed()
    {
        lookup.Setup(l => l.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("profile sidecar down"));

        var result = await resolver.ResolveAsync("tenant-1", "user-1");

        Assert.False(result.Resolved);
        Assert.Equal("recipient_resolution_unavailable", result.RejectionReason);
    }

    [Fact]
    public async Task Resolve_EmptyRecipient_FailsClosed()
    {
        var result = await resolver.ResolveAsync("tenant-1", "");

        Assert.False(result.Resolved);
        Assert.Equal("recipient_missing", result.RejectionReason);
        lookup.Verify(l => l.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
