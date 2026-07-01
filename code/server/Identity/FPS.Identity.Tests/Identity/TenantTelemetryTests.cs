using System.Diagnostics;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Infrastructure;
using FPS.SharedKernel.Observability;

namespace FPS.Identity.Tests.Identity;

// PLAT005B — the tenant observability dimension must come ONLY from the validated claim exposed by
// ICurrentUser (never from caller-supplied header/body/query), and platform/no-tenant contexts must
// use a sentinel that cannot be confused with a customer tenant.
public sealed class TenantTelemetryTests
{
    // The only input Resolve can read is ICurrentUser, whose TenantId is the validated JWT claim —
    // it has no access to raw request input, so a forged header/body value can never reach telemetry.
    private sealed class FakeCurrentUser(bool authenticated, string tenantId) : ICurrentUser
    {
        public string UserId => "u";
        public string TenantId => tenantId;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => authenticated;
        public string? DisplayName => null;
        public bool IsInRole(string role) => false;
    }

    [Fact]
    public void Resolve_AuthenticatedTenant_ReturnsClaimTenant()
        => Assert.Equal("acme", TenantTelemetry.Resolve(new FakeCurrentUser(true, "acme")));

    [Fact]
    public void Resolve_Unauthenticated_ReturnsNoTenantSentinel()
        => Assert.Equal(TenantTelemetry.NoTenant, TenantTelemetry.Resolve(new FakeCurrentUser(false, "acme")));

    [Fact]
    public void Resolve_AuthenticatedButNoTenant_ReturnsSentinel()
        => Assert.Equal(TenantTelemetry.NoTenant, TenantTelemetry.Resolve(new FakeCurrentUser(true, "")));

    [Fact]
    public void Resolve_NullUser_ReturnsSentinel()
        => Assert.Equal(TenantTelemetry.NoTenant, TenantTelemetry.Resolve(null));

    [Fact]
    public void NoTenantSentinel_CannotBeAValidTenantId()
        // Underscores are rejected by TenantStorageKey.Sanitise, so tenant_id="__none__" is
        // structurally impossible as a real tenant id and cannot be confused with a customer.
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.Sanitise(TenantTelemetry.NoTenant));

    [Fact]
    public void SetTenantTag_RealTenant_AddsSpanTag()
    {
        using var activity = new Activity("test").Start();
        TenantTelemetry.SetTenantTag(activity, "acme");
        Assert.Equal("acme", activity.GetTagItem(TenantTelemetry.TagName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetTenantTag_BlankTenant_LeavesSpanUnlabelled(string? tenantId)
    {
        using var activity = new Activity("test").Start();
        TenantTelemetry.SetTenantTag(activity, tenantId);
        Assert.Null(activity.GetTagItem(TenantTelemetry.TagName));
    }

    [Fact]
    public void SetTenantTag_NoTenantSentinel_LeavesSpanUnlabelled()
    {
        using var activity = new Activity("test").Start();
        TenantTelemetry.SetTenantTag(activity, TenantTelemetry.NoTenant);
        Assert.Null(activity.GetTagItem(TenantTelemetry.TagName));
    }

    [Fact]
    public void SetTenantTag_NullActivity_DoesNotThrow()
        => TenantTelemetry.SetTenantTag(null, "acme"); // no-op, no exception
}
