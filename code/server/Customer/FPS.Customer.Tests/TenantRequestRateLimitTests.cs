using System.Net;
using FPS.Customer.Controllers;

namespace FPS.Customer.Tests;

public sealed class TenantRequestRateLimitTests
{
    [Fact]
    public void ClientPartitionKey_PrefersTrustedCloudflareClientIp_NotTheProxyPeer()
    {
        // Behind the proxy the socket peer is the gateway (10.x); the real client comes via CF-Connecting-IP.
        var key = TenantRequestRateLimit.ClientPartitionKey("203.0.113.7", IPAddress.Parse("10.0.0.1"));
        Assert.Equal("203.0.113.7", key);
    }

    [Fact]
    public void ClientPartitionKey_FallsBackToRemoteIp_WhenNoCloudflareHeader()
    {
        // Local/dev (no Cloudflare): the socket peer IS the client.
        Assert.Equal("10.0.0.1", TenantRequestRateLimit.ClientPartitionKey(null, IPAddress.Parse("10.0.0.1")));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("")]
    [InlineData("   ")]
    public void ClientPartitionKey_IgnoresGarbageCloudflareHeader(string header)
    {
        // An unparseable header value is not trusted — fall back rather than partition on junk.
        Assert.Equal("10.0.0.2", TenantRequestRateLimit.ClientPartitionKey(header, IPAddress.Parse("10.0.0.2")));
    }

    [Fact]
    public void ClientPartitionKey_Unknown_WhenNeitherAvailable()
        => Assert.Equal("unknown", TenantRequestRateLimit.ClientPartitionKey(null, null));

    [Fact]
    public void ClientPartitionKey_DistinctClientsBehindSameProxy_GetDistinctBuckets()
    {
        // The whole point: two prospects behind the same gateway must not share one global window.
        var a = TenantRequestRateLimit.ClientPartitionKey("203.0.113.7", IPAddress.Parse("10.0.0.1"));
        var b = TenantRequestRateLimit.ClientPartitionKey("203.0.113.8", IPAddress.Parse("10.0.0.1"));
        Assert.NotEqual(a, b);
    }
}
