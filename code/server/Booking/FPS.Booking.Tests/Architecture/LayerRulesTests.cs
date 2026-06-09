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
        AssertNoDependency("FPS.Booking.Domain", "FPS.Booking.Controllers");
        AssertNoDependency("FPS.Booking.Domain", "FPS.Booking.Models");
        AssertNoDependency("FPS.Booking.Domain", "FPS.Booking.Identity");
        AssertNoDependency("FPS.Booking.Domain", "FPS.Booking.Simulation");
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
        AssertNoDependency("FPS.Booking.Application", "FPS.Booking.Controllers");
        AssertNoDependency("FPS.Booking.Application", "FPS.Booking.Models");
        AssertNoDependency("FPS.Booking.Application", "FPS.Booking.Identity");
        AssertNoDependency("FPS.Booking.Application", "FPS.Booking.Simulation");
    }

    private static void AssertNoDependency(string sourceNamespace, string forbiddenNamespace)
    {
        var result = Types.InAssembly(typeof(FPS.Booking.Domain.ValueObjects.UserId).Assembly)
            .That().ResideInNamespaceStartingWith(sourceNamespace)
            .ShouldNot().HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.IsSuccessful ? string.Empty :
        "Failing types:\n" + string.Join("\n", result.FailingTypeNames ?? []);
}
