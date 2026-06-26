using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Tests.Infrastructure;

public sealed class ConfigurationStorageKeyTests
{
    // ── config-policy key format ──────────────────────────────────────────────

    [Fact]
    public void PolicyKey_TenantDefault_HasExpectedFormat()
    {
        var key = TenantStorageKey.For("config-policy", "demo", "tenant-default");
        Assert.Equal("config-policy:demo:tenant-default", key);
    }

    [Fact]
    public void PolicyKey_LocationOverride_HasExpectedFormat()
    {
        var key = TenantStorageKey.For("config-policy-location", "demo", "prague");
        Assert.Equal("config-policy-location:demo:prague", key);
    }

    [Fact]
    public void PolicyKey_TenantDefault_StructurallyDistinctFromLocationNamedDefault()
    {
        var tenantDefaultKey = TenantStorageKey.For("config-policy", "demo", "tenant-default");
        var locationDefaultKey = TenantStorageKey.For("config-policy-location", "demo", "default");
        Assert.NotEqual(tenantDefaultKey, locationDefaultKey);
    }

    // ── config-slots key format ───────────────────────────────────────────────

    [Fact]
    public void SlotKey_HasExpectedFormat()
    {
        var key = TenantStorageKey.For("config-slots", "acme-corp", "main-office");
        Assert.Equal("config-slots:acme-corp:main-office", key);
    }

    // ── config-slotchange key format ──────────────────────────────────────────

    [Fact]
    public void SlotChangeKey_HasExpectedFormat()
    {
        var key = TenantStorageKey.For("config-slotchange", "demo", "prague");
        Assert.Equal("config-slotchange:demo:prague", key);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public void PolicyKey_DifferentTenants_ProduceDifferentKeys()
    {
        var keyDemo = TenantStorageKey.For("config-policy", "demo", "default");
        var keyOther = TenantStorageKey.For("config-policy", "other-co", "default");
        Assert.NotEqual(keyDemo, keyOther);
    }

    [Fact]
    public void SlotKey_DifferentTenants_ProduceDifferentKeys()
    {
        var keyA = TenantStorageKey.For("config-slots", "tenant-a", "prague");
        var keyB = TenantStorageKey.For("config-slots", "tenant-b", "prague");
        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void PolicyKey_TenantA_DoesNotContainTenantB()
    {
        var keyA = TenantStorageKey.For("config-policy", "tenant-a", "default");
        Assert.DoesNotContain("tenant-b", keyA);
    }

    // ── Tenant ID sanitisation ────────────────────────────────────────────────

    [Fact]
    public void TenantId_NormalisedToLowercase()
    {
        var key = TenantStorageKey.For("config-policy", "DEMO", "default");
        Assert.StartsWith("config-policy:demo:", key);
    }

    [Theory]
    [InlineData("fps-internal")]
    [InlineData("dapr-tenant")]
    [InlineData("admin-corp")]
    [InlineData("system-1")]
    public void ReservedTenantPrefix_Throws(string tenantId)
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.For("config-policy", tenantId, "default"));

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    public void TooShortTenantId_Throws(string tenantId)
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.For("config-policy", tenantId, "default"));
}
