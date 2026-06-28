using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;

namespace FPS.Customer.Tests.Identity;

public sealed class TenantAccessTests
{
    private sealed class FakeUser(string tenantId, params string[] roles) : ICurrentUser
    {
        public string UserId => "u1";
        public string TenantId => tenantId;
        public IReadOnlyList<string> Roles => roles;
        public bool IsAuthenticated => true;
        public string? DisplayName => null;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlatformAdmin_CanAdministerAnyTenant()
    {
        var user = new FakeUser("system", FpsRoles.PlatformAdmin);

        Assert.True(user.IsPlatformAdmin());
        Assert.True(user.CanAdministerTenant("acme"));
        Assert.True(user.CanAdministerTenant("globex"));
    }

    [Fact]
    public void TenantAdmin_CanAdministerOnlyOwnTenant()
    {
        var user = new FakeUser("acme", FpsRoles.Admin);

        Assert.False(user.IsPlatformAdmin());
        Assert.True(user.CanAdministerTenant("acme"));
        Assert.False(user.CanAdministerTenant("globex")); // the cross-tenant gap, now closed
    }

    [Fact]
    public void NonAdmin_CannotAdminister_EvenOwnTenant()
    {
        var user = new FakeUser("acme", FpsRoles.Employee, FpsRoles.HrManager);

        Assert.False(user.CanAdministerTenant("acme"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TenantAdmin_WithMissingRouteTenant_IsDenied(string? routeTenant)
    {
        var user = new FakeUser("acme", FpsRoles.Admin);

        Assert.False(user.CanAdministerTenant(routeTenant));
    }
}
