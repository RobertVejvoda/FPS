using FPS.Booking.Infrastructure;

namespace FPS.Booking.Infrastructure.Tests;

public sealed class TenantStorageKeyTests
{
    // ── Sanitise: valid inputs ────────────────────────────────────────────────

    [Theory]
    [InlineData("demo", "demo")]
    [InlineData("acme-corp", "acme-corp")]
    [InlineData("ACME", "acme")]
    [InlineData("  tenant1  ", "tenant1")]
    [InlineData("t12", "t12")]
    public void Sanitise_ValidInput_ReturnsNormalisedId(string input, string expected)
    {
        var result = TenantStorageKey.Sanitise(input);
        Assert.Equal(expected, result);
    }

    // ── Sanitise: invalid inputs ──────────────────────────────────────────────

    [Theory]
    [InlineData("ab")]           // too short
    [InlineData("a")]            // too short
    [InlineData("")]             // empty
    [InlineData("   ")]          // whitespace only
    public void Sanitise_TooShort_Throws(string input)
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.Sanitise(input));

    [Fact]
    public void Sanitise_TooLong_Throws()
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.Sanitise(new string('a', 64)));

    [Theory]
    [InlineData("fps-internal")]   // reserved prefix
    [InlineData("dapr-tenant")]    // reserved prefix
    [InlineData("admin-corp")]     // reserved prefix
    [InlineData("system-1")]       // reserved prefix
    public void Sanitise_ReservedPrefix_Throws(string input)
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.Sanitise(input));

    [Theory]
    [InlineData("tenant_1")]    // underscore not allowed
    [InlineData("tenant.1")]    // dot not allowed
    [InlineData("-tenant")]     // leading hyphen
    [InlineData("tenant-")]     // trailing hyphen
    [InlineData("tenant 1")]    // space not allowed
    public void Sanitise_InvalidCharacters_Throws(string input)
        => Assert.Throws<ArgumentException>(() => TenantStorageKey.Sanitise(input));

    // ── For: key format ───────────────────────────────────────────────────────

    [Fact]
    public void For_String_ProducesExpectedKeyFormat()
    {
        var key = TenantStorageKey.For("request", "demo", "abc-123");
        Assert.Equal("request:demo:abc-123", key);
    }

    [Fact]
    public void For_Guid_ProducesExpectedKeyFormat()
    {
        var id = new Guid("11111111-1111-1111-1111-111111111111");
        var key = TenantStorageKey.For("request", "acme-corp", id);
        Assert.Equal($"request:acme-corp:{id}", key);
    }

    [Fact]
    public void For_NormalisesTenantId()
    {
        var key = TenantStorageKey.For("penalty", "ACME", "r1:NoShow");
        Assert.StartsWith("penalty:acme:", key);
    }

    // ── Tenant isolation: different tenants produce different keys ────────────

    [Fact]
    public void For_DifferentTenants_SameEntityId_ProduceDifferentKeys()
    {
        var id = Guid.NewGuid();
        var key1 = TenantStorageKey.For("request", "tenant-a", id);
        var key2 = TenantStorageKey.For("request", "tenant-b", id);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void For_SameTenant_DifferentEntityIds_ProduceDifferentKeys()
    {
        var key1 = TenantStorageKey.For("request", "demo", Guid.NewGuid());
        var key2 = TenantStorageKey.For("request", "demo", Guid.NewGuid());
        Assert.NotEqual(key1, key2);
    }

    // ── No cross-tenant read: a tenant-a key cannot be constructed from tenant-b context ──

    [Fact]
    public void For_TenantAKey_DoesNotMatchTenantBKey_WithSameEntityId()
    {
        var sharedId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var keyA = TenantStorageKey.For("request", "tenant-a", sharedId);
        var keyB = TenantStorageKey.For("request", "tenant-b", sharedId);

        // A reader using tenant-b context cannot accidentally address tenant-a data
        Assert.DoesNotContain("tenant-a", keyB);
        Assert.DoesNotContain("tenant-b", keyA);
    }
}
