using NetArchTest.Rules;

namespace FPS.Booking.Tests.Architecture;

public class LayerRulesTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith("FPS.Booking.Domain")
            .ShouldNot().HaveDependencyOn("FPS.Booking.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith("FPS.Booking.Domain")
            .ShouldNot().HaveDependencyOn("FPS.Booking.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith("FPS.Booking.Domain")
            .ShouldNot().HaveDependencyOn("FPS.Booking.API")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith("FPS.Booking.Application")
            .ShouldNot().HaveDependencyOn("FPS.Booking.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith("FPS.Booking.Application")
            .ShouldNot().HaveDependencyOn("FPS.Booking.API")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.IsSuccessful ? string.Empty :
        "Failing types:\n" + string.Join("\n", result.FailingTypeNames ?? []);
}
