using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class CustomerStorageKeyTests
{
    [Theory]
    [InlineData("demo")]
    [InlineData("acme-corp")]
    [InlineData("t123")]
    public void Tenant_ValidId_ProducesCorrectKeyFormat(string tenantId)
    {
        var key = CustomerStorageKey.Tenant(tenantId);

        Assert.Equal($"tenant:{tenantId}", key);
    }

    [Fact]
    public void Tenant_UpperCaseId_NormalisesToLowercase()
    {
        var key = CustomerStorageKey.Tenant("DEMO");

        Assert.Equal("tenant:demo", key);
    }

    [Fact]
    public void TenantSlug_ProducesCorrectKeyFormat()
    {
        var key = CustomerStorageKey.TenantSlug("demo-company");

        Assert.Equal("tenant:slug:demo-company", key);
    }

    [Fact]
    public void IdentityConfig_ProducesCorrectKeyFormat()
    {
        var key = CustomerStorageKey.IdentityConfig("demo");

        Assert.Equal("identity:config:demo", key);
    }

    [Fact]
    public void IdentityAdmins_ProducesCorrectKeyFormat()
    {
        var key = CustomerStorageKey.IdentityAdmins("demo");

        Assert.Equal("identity:admins:demo", key);
    }

    [Fact]
    public void Bootstrap_ProducesCorrectKeyFormat()
    {
        var key = CustomerStorageKey.Bootstrap("demo");

        Assert.Equal("bootstrap:demo", key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Sanitise_InvalidId_Throws(string tenantId)
    {
        Assert.Throws<ArgumentException>(() => CustomerStorageKey.Tenant(tenantId));
    }

    [Theory]
    [InlineData("fairspot-internal")]
    [InlineData("dapr-state")]
    [InlineData("admin-tenant")]
    [InlineData("system-core")]
    public void Sanitise_ReservedPrefix_Throws(string tenantId)
    {
        Assert.Throws<ArgumentException>(() => CustomerStorageKey.Tenant(tenantId));
    }

    [Fact]
    public void Sanitise_TooLong_Throws()
    {
        var longId = new string('a', 64);
        Assert.Throws<ArgumentException>(() => CustomerStorageKey.Tenant(longId));
    }

    [Fact]
    public void Sanitise_InvalidChars_Throws()
    {
        Assert.Throws<ArgumentException>(() => CustomerStorageKey.Tenant("tenant with spaces"));
    }

    [Fact]
    public void AllKeyTypes_UseDifferentPrefixes()
    {
        var tenantKey = CustomerStorageKey.Tenant("demo");
        var slugKey = CustomerStorageKey.TenantSlug("demo");
        var configKey = CustomerStorageKey.IdentityConfig("demo");
        var adminsKey = CustomerStorageKey.IdentityAdmins("demo");
        var bootstrapKey = CustomerStorageKey.Bootstrap("demo");

        var keys = new[] { tenantKey, slugKey, configKey, adminsKey, bootstrapKey };
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }
}
