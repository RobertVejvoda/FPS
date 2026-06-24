using FPS.Profile.Identity;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Xunit;

namespace FPS.Profile.Tests;

public sealed class CurrentUserDisplayNameTests
{
    [Fact]
    public void DisplayName_NameClaimPresent_ReturnsName()
    {
        var sut = MakeCurrentUser(new Claim("name", "Alice Smith"));
        Assert.Equal("Alice Smith", sut.DisplayName);
    }

    [Fact]
    public void DisplayName_NameClaimWithWhitespace_Trimmed()
    {
        var sut = MakeCurrentUser(new Claim("name", "  Bob Jones  "));
        Assert.Equal("Bob Jones", sut.DisplayName);
    }

    [Fact]
    public void DisplayName_GivenAndFamilyName_CombinedWithSpace()
    {
        var sut = MakeCurrentUser(
            new Claim("given_name", "Alice"),
            new Claim("family_name", "Smith"));
        Assert.Equal("Alice Smith", sut.DisplayName);
    }

    [Fact]
    public void DisplayName_NameClaimTakesPrecedenceOverGivenFamily()
    {
        var sut = MakeCurrentUser(
            new Claim("name", "Full Name Claim"),
            new Claim("given_name", "Given"),
            new Claim("family_name", "Family"));
        Assert.Equal("Full Name Claim", sut.DisplayName);
    }

    [Fact]
    public void DisplayName_NoNameClaims_ReturnsNull()
    {
        var sut = MakeCurrentUser(new Claim("sub", "user-id-123"));
        Assert.Null(sut.DisplayName);
    }

    [Fact]
    public void DisplayName_OnlyGivenName_ReturnsTrimmedValue()
    {
        var sut = MakeCurrentUser(new Claim("given_name", "Alice"));
        Assert.Equal("Alice", sut.DisplayName);
    }

    [Fact]
    public void DisplayName_EmptyNameClaim_FallsBackToGivenFamily()
    {
        var sut = MakeCurrentUser(
            new Claim("name", "   "),
            new Claim("given_name", "Alice"),
            new Claim("family_name", "Smith"));
        Assert.Equal("Alice Smith", sut.DisplayName);
    }

    private static FPS.Profile.Identity.CurrentUser MakeCurrentUser(params Claim[] claims)
    {
        var allClaims = new List<Claim>(claims)
        {
            new("sub", "user-1"),
            new("tenant_id", "tenant-1"),
        };
        var identity = new ClaimsIdentity(allClaims, "test");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);

        return new FPS.Profile.Identity.CurrentUser(accessor.Object);
    }
}
